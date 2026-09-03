using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Azure.Monitor.OpenTelemetry.Exporter;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.Colaboradores;
using Cosmos.EventDriven.CritterStack;
using Cosmos.EventDriven.CritterStack.AzureServiceBus;
using Cosmos.EventSourcing.CritterStack;
using Cosmos.EventSourcing.CritterStack.Commands;
using Bitakora.ControlAsistencia.TenantResolver;
using FluentValidation;
using Marten;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Bitakora.ControlAsistencia.Colaboradores.Infraestructura;

// Issue #360: composicion del contenedor DI del dominio Colaboradores, extraida de Program.cs a un
// metodo testeable. Unica fuente de verdad: Program.cs y el test de composicion
// (Colaboradores.Tests) invocan este mismo metodo -- mismo patron que ComposicionServicios de
// Programacion/ControlHoras (issue #221), asi que un wiring roto (p.ej. el hueco de
// ITenantResolver de #219) no puede desincronizarse entre el host real y el guardrail de CI.
public static class ComposicionServicios
{
    public static IServiceCollection AgregarServiciosColaboradores(
        this IServiceCollection services,
        string martenConnectionString,
        string serviceBusConnectionString,
        bool isDev)
    {
        services.AgregarWolverineParaComandosServerless(
            typeof(IColaboradoresAssemblyMarker).Assembly,
            martenConnectionString,
            "colaboradores",
            isDev,
            options =>
            {
                // Issue #309 (replicado desde el scaffold del dominio -- Programacion/ControlHoras
                // ya lo aplican): apaga el polling de metricas de profundidad de cola de Wolverine
                // (PersistenceMetrics.StartPolling, PeriodicTimer de 5s que llama
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

                // El dominio nace sin ningun evento de bus (issue #360: solo se genera el andamiaje
                // sobre el que se implementan los cortes #348/#353 y el resto del desglose). Cuando
                // Colaboradores publique su primer evento (privado o publico), se registra aqui con
                // options.PublicarEventoServerless<TEvento>("<topic>") -- ADR-0024 decision #3.
            });

        services.AgregarMartenEventStore();
        // Tenancy (MEF-ADR-0028 etapa b): identidad ambiente por AsyncLocal, poblada por
        // TenantContextMiddleware en Program.cs (patron de Cosmos.ControlPlane, MEF-ADR-0032). NO usar
        // AgregarTenantResolverHibrido(): su ProxyTenantResolver decide la rama en el constructor
        // segun IHttpContextAccessor.HttpContext, que es null cuando el grafo de DI lo construye en el
        // worker aislado -- toda request HTTP caia en la rama de Wolverine y fallaba (hotfix 2026-09-01).
        services.AgregarTenantResolverControlAsistencia();
        services.AgregarWolverineCommandRouter();
        services.AgregarWolverineEventSender();
        // Nota: AgregarWolverinePrivateEventRouter() no se registra todavia -- el dominio no
        // consume ningun evento privado por ahora (mismo criterio que Programacion). Se agrega
        // junto con el primer FunctionEndpoint de ServiceBus que este dominio necesite.

        services.ConfigureMarten(options =>
        {
            // Issue #277 (replicado desde el scaffold): registra los tipos de evento persistidos en
            // el EventGraph. Lista vacia al nacer -- se llena a medida que ColaboradorAggregateRoot
            // aplique sus primeros eventos.
            options.Events.AddEventTypes(IdentidadEventosColaboradores.TiposPersistidos);

            // Issue #294 (aplicado a FichaColaborador por #356, mismo patron que #328 aplico a
            // TurnoVigente en ControlHoras): declara del lado LECTURA la forma de mt_version que el
            // worker ya impone del lado escritura -- aqui la tabla la posee la proyeccion y este
            // Function App solo la consulta (ObtenerFichaColaborador).
            //
            // Marten aplica ProjectionDocumentPolicy a todo documento que sea target de una
            // proyeccion registrada en ese store: UseNumericRevisions = true, Metadata.Revision
            // (mt_version bigint) habilitada y Metadata.Version (mt_version uuid) DESHABILITADA. No
            // es opt-in ni depende de IRevisioned ni del lifecycle: la doc lo declara como la
            // excepcion a que el versionado sea opt-in
            // (https://martendb.io/documents/concurrency, "Numeric Revisioned Documents") y el
            // codigo lo hace incondicional (Marten/Events/Projections/ProjectionDocumentPolicy.cs).
            //
            // El worker registra FichaColaboradorProjection, asi que crea mt_version como bigint.
            // Este store NO la registra ni puede hacerlo -- FichaColaboradorProjection vive en el
            // ensamblado del worker y referenciarlo violaria CA-ADR-0029 --, asi que sin esta linea
            // esperaria mt_version uuid sobre la MISMA tabla fisica. Con AutoCreate en su default
            // CreateOrUpdate el sintoma no es un 404: Marten intenta "alter column mt_version type
            // uuid" en CADA request, Postgres lo rechaza con 42804 (no hay cast automatico
            // bigint -> uuid) y los GET responden 500 de forma permanente. Eso fue lo que ocurrio en
            // dev tras el deploy de #290 sobre el read model que #323 ya retiro.
            //
            // Se declara por documento y no via Policies para no alterar la forma de ningun otro
            // documento de este store. El par de config-tests (este lado y el del worker) congela
            // los mismos valores literales.
            options.Schema.For<FichaColaborador>().UseNumericRevisions(true);

            // Issue #357: MISMA razon que la linea de arriba, para la segunda vista materializada
            // del dominio -- el worker registra CategoriaDeEtiquetasProjection (N2) y crea
            // mt_version como bigint; este Function App la consulta con session.Query en
            // ListarCategoriasDeEtiquetas. Sin esta linea el GET responde 500 permanente en dev
            // (42804 por request), no un 404: el mismo modo de falla que #294 dejo documentado
            // arriba. Cada vista materializada que este store consulte necesita su propia
            // declaracion -- el par de config-tests de ambos lados la congela.
            options.Schema.For<CategoriaDeEtiquetas>().UseNumericRevisions(true);

            // Tercera vista materializada del dominio, misma razon que las dos lineas de arriba:
            // el worker la crea con mt_version bigint y ListarDirectorioColaboradores la consulta.
            options.Schema.For<DirectorioColaborador>().UseNumericRevisions(true);

            // Issue #330: registra la serializacion custom de los VOs con ctor privado que aparecen
            // como payload de eventos persistidos (Identificacion, NombreColaborador) -- AQUI
            // DENTRO junto a AddEventTypes -- nunca en un ConfigureMarten separado (issue #232 CA-5:
            // ComposicionServiciosTests lo verifica sobre el store real del contenedor, y un segundo
            // ConfigureMarten corre el riesgo de que el resolver de serializacion custom quede sin
            // efecto en silencio si el orden de callbacks cambia). Mismo patron que
            // ControlHoras/Programacion: reconfigura el SystemTextJsonSerializer que Marten ya
            // registro con un resolver custom.
            if (options.Serializer() is Marten.Services.SystemTextJsonSerializer stj)
            {
                stj.Configure(jsonOptions =>
                {
                    var resolver = new DefaultJsonTypeInfoResolver();
                    ConfiguracionSerializacionColaboradores.ConfigurarResolver(resolver);
                    jsonOptions.TypeInfoResolver = resolver;
                });
            }
        });

        // Observabilidad (issue #308, CA-ADR-0009 Capa 2): mismo wiring que ControlHoras -- el
        // dominio de referencia mas reciente en adoptar el exporter completo (a diferencia de
        // Programacion, que todavia no lo tiene). Ratio configurable via TELEMETRY_SAMPLING_RATIO
        // (default 0.2, igual que los otros dos dominios); el daily cap (Capa 3) y la alerta de
        // spike (Capa 4) siguen siendo el respaldo si el sampling deja pasar demasiado.
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
                .AddSource("Bitakora.ControlAsistencia.Colaboradores.*"))
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
        services.AddValidatorsFromAssemblyContaining<IColaboradoresAssemblyMarker>();

        // Issue #399: readiness gate del event store, consumido por ReadyCheck (GET /api/ready).
        services.AddScoped<IEventStoreReadinessProbe, EventStoreReadinessProbe>();

        return services;
    }
}
