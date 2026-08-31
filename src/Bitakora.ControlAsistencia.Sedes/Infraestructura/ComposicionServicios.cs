using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Azure.Monitor.OpenTelemetry.Exporter;
using Bitakora.ControlAsistencia.PrivateEvents.Sedes;
using Bitakora.ControlAsistencia.ReadModels.Sedes;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using Bitakora.ControlAsistencia.Sedes.InstalarDispositivoFunction;
using Bitakora.ControlAsistencia.Sedes.ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado;
using Cosmos.EventDriven.CritterStack;
using Cosmos.EventDriven.CritterStack.AzureServiceBus;
using Cosmos.EventSourcing.CritterStack;
using Cosmos.EventSourcing.CritterStack.Commands;
using Cosmos.MultiTenancy.CritterStack;
using FluentValidation;
using Marten;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Bitakora.ControlAsistencia.Sedes.Infraestructura;

// Issue #455: composicion del contenedor DI del dominio Sedes, extraida de Program.cs a un metodo
// testeable. Unica fuente de verdad: Program.cs y el test de composicion (Sedes.Tests) invocan
// este mismo metodo -- mismo patron que ComposicionServicios de Programacion/ControlHoras/
// Colaboradores (issue #221/#360), asi que un wiring roto (p.ej. el hueco de ITenantResolver de
// #219) no puede desincronizarse entre el host real y el guardrail de CI.
public static class ComposicionServicios
{
    public static IServiceCollection AgregarServiciosSedes(
        this IServiceCollection services,
        string martenConnectionString,
        string serviceBusConnectionString,
        bool isDev)
    {
        services.AgregarWolverineParaComandosServerless(
            typeof(ISedesAssemblyMarker).Assembly,
            martenConnectionString,
            "sedes",
            isDev,
            options =>
            {
                // Issue #309 (replicado desde el scaffold del dominio -- Programacion/ControlHoras/
                // Colaboradores ya lo aplican): apaga el polling de metricas de profundidad de cola
                // de Wolverine (PersistenceMetrics.StartPolling, PeriodicTimer de 5s que llama
                // store.Admin.FetchCountsAsync()). Nadie las consume hoy (sin dashboard ni alerta)
                // y CheckHealthAsync llama FetchCountsAsync por su cuenta, asi que el health check
                // no depende de este polling.
                //
                // Se conserva la durabilidad real -- recovery, scheduled jobs y dead letters, que
                // corren en el mismo DurabilityAgent: DurabilityAgentEnabled y Mode no se tocan.
                //
                // Va en el callback de AgregarWolverineParaComandosServerless, y no despues de esa
                // llamada, porque es el hook que el paquete expone sobre WolverineOptions:
                // Cosmos.EventSourcing.CritterStack 2.3.1 lo corre PRIMERO y despues solo reasigna
                // Durability.Mode = Solo -- esa reasignacion pisaria en silencio cualquier intento
                // de tocar Mode desde aqui, pero no toca DurabilityMetricsEnabled (verificado
                // descompilando el paquete en ControlHoras/ComposicionServicios). Que sobreviva no
                // es contrato del paquete: lo sostiene el guardrail de ComposicionServiciosTests
                // sobre el contenedor real.
                options.Durability.DurabilityMetricsEnabled = false;

                options.HabilitarAzureServiceBusParaServerLess(serviceBusConnectionString);

                // Issue #467: primer evento de bus del dominio (MEF-ADR-0024 decision #3).
                options.PublicarEventoServerless<SedeDeMarcacionResuelta>("sede-de-marcacion-resuelta");
            });

        services.AgregarMartenEventStore();
        // Tenancy (MEF-ADR-0028 etapa b, migrado por /install-apim -- issue #337/#340): resolver real
        // basado en TenantContext (header-based via HttpContext, o WolverineMessageContextTenantResolver
        // dentro de handlers de Wolverine sin HttpContext). El mapping claim -> header (user_email ->
        // X-User-Id, tenant_id -> X-Tenant-Id) ya lo normaliza la politica global del gateway APIM
        // (MEF-ADR-0032 seccion 4/5) -- a diferencia del auto-cableo generico de domain-scaffolder,
        // esta migracion NO deja ningun TODO de mapping de claims por dominio: queda resuelto por
        // construccion (MEF-ADR-0028 seccion 4).
        services.AgregarTenantResolverHibrido();
        services.AgregarWolverineCommandRouter();
        services.AgregarWolverineEventSender();
        // Issue #467: primer FunctionEndpoint de ServiceBus de este dominio
        // (ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado) -- se registra el router de
        // eventos privados (MEF-ADR-0024 decision #8).
        services.AgregarWolverinePrivateEventRouter();
        services.AddScoped<ILectorSedesParaMarcacion, LectorReadSideSedes>();
        // Puerto segregado del rechazo cross-sede de InstalarDispositivo: mismo lookup, misma
        // implementacion concreta que ILectorSedesParaMarcacion.
        services.AddScoped<ILectorUbicacionDispositivo, LectorReadSideSedes>();

        services.ConfigureMarten(options =>
        {
            // Issue #277 (replicado desde el scaffold): registra los tipos de evento persistidos en
            // el EventGraph. Issue #456: la lista estrena su primer tipo (SedeRegistrada).
            options.Events.AddEventTypes(IdentidadEventosSedes.TiposPersistidos);

            // Issue #461 (par 2 de compatibilidad write-side/read-side, MEF-ADR-0034 seccion 6;
            // mismo patron que #294/#328/#356 dejaron en Colaboradores y ControlHoras): declara del
            // lado LECTURA la forma de mt_version que el worker ya impone del lado escritura -- la
            // tabla de FichaSede la posee la proyeccion y este Function App solo la consulta
            // (ObtenerFichaSede, ListarFichasSede).
            //
            // Marten aplica ProjectionDocumentPolicy a todo documento target de una proyeccion
            // registrada en ese store: UseNumericRevisions = true, Metadata.Revision (mt_version
            // bigint) habilitada y Metadata.Version (mt_version uuid) DESHABILITADA -- incondicional
            // (https://martendb.io/documents/concurrency, "Numeric Revisioned Documents"). Este
            // store no registra FichaSedeProjection ni puede hacerlo (vive en el worker,
            // CA-ADR-0029), asi que sin esta linea esperaria mt_version uuid sobre la MISMA tabla
            // fisica: Marten intenta "alter column mt_version type uuid" en CADA request, Postgres
            // lo rechaza con 42804 y los GET responden 500 permanente -- no 404. Es lo que ocurrio
            // en dev tras el deploy de #290.
            //
            // El par de config-tests (este lado y el del worker) congela los mismos literales.
            options.Schema.For<FichaSede>().UseNumericRevisions(true);

            // Issue #467: misma declaracion del par 2 para UbicacionDispositivo, que este Function
            // App empieza a consultar aqui (LectorReadSideSedes.BuscarUbicacionAsync, la
            // reaccion de MEF-ADR-0046). El worker ya la materializa con mt_version bigint via
            // UbicacionDispositivoProjection; sin esta linea el store esperaria mt_version uuid
            // sobre la misma tabla y cada lookup dispararia el "alter column" que Postgres rechaza
            // con 42804 -- la reaccion terminaria siempre en dead-letter.
            options.Schema.For<UbicacionDispositivo>().UseNumericRevisions(true);

            // Issue #456: instala el resolver de serializacion del dominio -- AQUI DENTRO junto a
            // AddEventTypes, nunca en un ConfigureMarten separado (issue #232 CA-5:
            // ComposicionServiciosTests lo verifica sobre el store real del contenedor, y un segundo
            // ConfigureMarten corre el riesgo de que el resolver quede sin efecto en silencio si el
            // orden de callbacks cambia). Hoy ConfigurarResolver no registra ningun tipo
            // (SedeRegistrada es un record plano), pero la llamada se cablea ya: el dia que
            // #457-#461 agreguen un VO con ctor privado, registrarlo en ConfiguracionSerializacionSedes
            // basta para que el write-side lo use -- sin esta linea el round-trip de
            // SedeRegistradaSerializacionTests seguiria verde mientras Marten reventaria en runtime.
            // Mismo patron que Colaboradores/ControlHoras/Programacion.
            if (options.Serializer() is Marten.Services.SystemTextJsonSerializer stj)
            {
                stj.Configure(jsonOptions =>
                {
                    var resolver = new DefaultJsonTypeInfoResolver();
                    ConfiguracionSerializacionSedes.ConfigurarResolver(resolver);
                    jsonOptions.TypeInfoResolver = resolver;
                });
            }
        });

        // Observabilidad (issue #308, CA-ADR-0009 Capa 2): mismo wiring que ControlHoras/
        // Colaboradores. Ratio configurable via TELEMETRY_SAMPLING_RATIO (default 0.2, igual que
        // los otros dominios); el daily cap (Capa 3) y la alerta de spike (Capa 4) siguen siendo el
        // respaldo si el sampling deja pasar demasiado.
        var samplingRatio = double.TryParse(
            Environment.GetEnvironmentVariable("TELEMETRY_SAMPLING_RATIO"),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var ratio) && ratio is >= 0.0 and <= 1.0
                ? ratio
                : 0.2;

        // Issue #308 hallazgo 1: UseAzureMonitorExporter() llama SetSampler internamente
        // (Azure.Monitor.OpenTelemetry.Exporter 1.8.1) con RateLimitedSampler porque
        // AzureMonitorExporterOptions.TracesPerSecond tiene default 5.0. El SetSampler propio DEBE
        // ir en un segundo .WithTracing(...) DESPUES de .UseAzureMonitorExporter() o ese
        // SetSampler interno lo pisa y el sampler configurado aqui nunca se instala (verificado en
        // runtime sobre ControlHoras).
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddSource("Wolverine")
                .AddSource("Marten")
                .AddSource("Npgsql")
                .AddSource("Bitakora.ControlAsistencia.Sedes.*"))
            .UseFunctionsWorkerDefaults()
            // MEF-ADR-0038 seccion 9 (extendida al write-side, harness #700): con el flip en su
            // default (true) el exporter instala LogFilteringProcessor, que descarta todo LogRecord
            // emitido dentro de un span no muestreado. Con TELEMETRY_SAMPLING_RATIO fraccionario
            // (default 0.2 aqui) eso pierde en silencio los LogError de los handlers, Wolverine y
            // Marten -- justo la senal que la alerta exception_spike (CA-ADR-0009 Capa 4) mira.
            .UseAzureMonitorExporter(o => o.EnableTraceBasedLogsSampler = false)
            .WithTracing(tracing => tracing
                .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingRatio))))
            // Issue #515 (CA-ADR-0009): UseAzureMonitorExporter() activa el pipeline de metricas sin
            // opt-out por senal (WithMetrics interno) -- ~76.000 metricas/dia de runtime/ASP.NET Core
            // (dotnet.gc.*, kestrel.*, etc.) que nadie consulta, medidas en el episodio de
            // ingestion-warning. El wildcard cubre cualquier instrumento, incluso uno que aparezca en
            // un futuro upgrade del exporter.
            .WithMetrics(metrics => metrics.AddView(instrumentName: "*", MetricStreamConfiguration.Drop));

        // El View de arriba filtra MEDICIONES, no evita que UseAzureMonitorExporter() construya su
        // reader de metricas: a diferencia del de trazas (diferido a un hosted service), ese se
        // construye de forma SINCRONICA al resolver MeterProvider y su ctor exige una connection
        // string o lanza (decompilado de Azure.Monitor.OpenTelemetry.Exporter 1.8.1). Sin este
        // fallback, un arranque en frio con la Key Vault reference aun sin resolver tumbaria el
        // Function App entero -- el mismo escenario que el TracerProvider ya tolera (CA-ADR-0009,
        // actualizacion 2026-06-18). El valor es inerte: con todas las metricas dropeadas, el reader
        // nunca tiene nada que exportar. Que nunca pise una connection string real no es una promesa
        // de este comentario sino de AgregarServicios*_ConservaLaConnectionStringReal.
        services.PostConfigure<AzureMonitorExporterOptions>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
                options.ConnectionString =
                    "InstrumentationKey=00000000-0000-0000-0000-000000000000;" +
                    "IngestionEndpoint=https://dummy.in.applicationinsights.azure.com/";
        });

        // Serializacion JSON global: camelCase hacia el cliente, case-insensitive en lectura
        services.Configure<JsonSerializerOptions>(options =>
        {
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.PropertyNameCaseInsensitive = true;
        });

        // Validacion de requests
        services.AddScoped<IRequestValidator, RequestValidator>();
        services.AddValidatorsFromAssemblyContaining<ISedesAssemblyMarker>();

        // Issue #399 (replicado): readiness gate del event store, consumido por ReadyCheck
        // (GET /api/ready).
        services.AddScoped<IEventStoreReadinessProbe, EventStoreReadinessProbe>();

        return services;
    }
}
