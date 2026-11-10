using System.Text.Json;
using Azure.Monitor.OpenTelemetry.Exporter;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
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

namespace Bitakora.ControlAsistencia.ControlHoras.Infraestructura;

// Program.cs y el test de composicion (ControlHoras.Tests) invocan este mismo metodo, asi que un
// wiring roto no puede desincronizarse entre el host real y el guardrail de CI.
public static class ComposicionServicios
{
    public static IServiceCollection AgregarServiciosControlHoras(
        this IServiceCollection services,
        string martenConnectionString,
        string serviceBusConnectionString,
        bool isDev)
    {
        services.AgregarWolverineParaComandosServerless(
            typeof(IControlHorasAssemblyMarker).Assembly,
            martenConnectionString,
            "control_horas",
            isDev,
            options =>
            {
                // Apaga el polling de metricas de profundidad de cola de Wolverine (PeriodicTimer de
                // 5 s que llama store.Admin.FetchCountsAsync()): 4 de las 8 consultas Postgres
                // repetitivas medidas en dev, sin consumidor. CheckHealthAsync llama
                // FetchCountsAsync por su cuenta, asi que el health check no depende de este polling.
                // La durabilidad real (recovery, scheduled jobs, dead letters) no se toca.
                //
                // Va en este callback porque Cosmos.EventSourcing.CritterStack 2.3.1 lo corre
                // primero y despues reasigna Durability.Mode = Solo, sin tocar
                // DurabilityMetricsEnabled (verificado descompilando el paquete). Que sobreviva no es
                // contrato del paquete: lo sostiene el guardrail de ComposicionServiciosTests.
                options.Durability.DurabilityMetricsEnabled = false;

                options.HabilitarAzureServiceBusParaServerLess(serviceBusConnectionString);
                // ADR-0004 + ADR-0005: un topic por evento, naming kebab-case en participio.
                options.PublicarEventoServerless<DiaDepurado>("dia-depurado");
                options.PublicarEventoServerless<DiasDepurados>("dias-depurados");
                // Cruza fisicamente el ASB interno aunque lo consuma este mismo Function App (fan-in
                // ActualizarControlDiario). Topic y subscription en infra/environments/dev/main.tf.
                options.PublicarEventoServerless<RegistroDeMarcacionCreado>("registro-de-marcacion-creado");
            });

        services.AgregarMartenEventStore();
        // Identidad ambiente por AsyncLocal, poblada por TenantContextMiddleware en Program.cs
        // (CA-ADR-0032). NO usar AgregarTenantContextHibrido(): su ProxyTenantContext decide la rama
        // en el constructor segun IHttpContextAccessor.HttpContext, que es null cuando el grafo de DI
        // lo construye en el worker aislado, y toda request HTTP cae en la rama de Wolverine y falla.
        services.AgregarTenantResolverControlAsistencia();
        // AgregarWolverineCommandRouter se conserva: RegistrarMarcacion (HTTP) lo sigue usando.
        services.AgregarWolverineCommandRouter();
        services.AgregarWolverinePrivateEventRouter();
        services.AgregarWolverineEventSender();

        // Las columnas de metadata de evento de MEF-ADR-0034 seccion 7 (CorrelationId/CausationId/
        // Headers) no se habilitan aqui: las fija AgregarConfiguracionMartenComandos desde
        // Cosmos.EventSourcing.CritterStack v2.3.1, y ComposicionServiciosTests lo verifica.
        services.ConfigureMarten(options =>
        {
            // No declara alias: ver IdentidadEventosControlHoras.
            options.Events.AddEventTypes(IdentidadEventosControlHoras.TiposPersistidos);

            // Declara del lado lectura la forma de mt_version que el worker impone del lado escritura.
            // Marten aplica ProjectionDocumentPolicy (UseNumericRevisions, mt_version bigint) a todo
            // documento target de una proyeccion registrada en su store, sin opt-in
            // (https://martendb.io/documents/concurrency, "Numeric Revisioned Documents"). Este store
            // no registra TurnoVigenteProjection (vive en el worker, CA-ADR-0029), asi que sin esta
            // linea esperaria mt_version uuid sobre la misma tabla: con AutoCreate CreateOrUpdate
            // Marten intenta "alter column mt_version type uuid" en cada request, Postgres lo rechaza
            // con 42804 y los GET responden 500 de forma permanente.
            //
            // Se declara por documento y no via Policies para no alterar otros documentos de este
            // store. El par de config-tests (este lado y el del worker) congela los mismos valores.
            options.Schema.For<TurnoVigente>().UseNumericRevisions(true);

            // Mismo caso que TurnoVigente: sin esta linea ListarAsistenciasDiarias y
            // ListarResumenesAsistencia responden 500 por el "alter column" 42804.
            options.Schema.For<AsistenciaDiaria>().UseNumericRevisions(true);

            // Mismo caso que TurnoVigente: sin esta linea ListarAdvertenciasProgramacionSemanal
            // responde 500 por el "alter column" 42804.
            options.Schema.For<AdvertenciasProgramacionSemanal>().UseNumericRevisions(true);

            if (options.Serializer() is Marten.Services.SystemTextJsonSerializer stj)
            {
                stj.Configure(jsonOptions =>
                {
                    var resolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver();
                    // Por que son dos listas: ver ConfiguracionSerializacionCalculoHoras.
                    ConfiguracionSerializacionControlHoras.ConfigurarResolver(resolver);
                    ConfiguracionSerializacionCalculoHoras.ConfigurarResolver(resolver);
                    jsonOptions.TypeInfoResolver = resolver;
                });
            }
        });

        // Se usa el exporter, no el distro Azure.Monitor.OpenTelemetry.AspNetCore, para evitar
        // request telemetry duplicado en el worker.
        //
        // ADR-0009 Capa 2 (control de costos): con telemetryMode=OpenTelemetry el sampling de
        // host.json deja de aplicar, asi que el volumen de trazas se controla aqui con
        // TELEMETRY_SAMPLING_RATIO (default 0.2; para un diagnostico puntual se sube a 1.0). El
        // sampling head-based tambien muestrea excepciones; el daily cap (Capa 3) y la alerta de
        // spike (Capa 4) lo compensan.
        var samplingRatio = double.TryParse(
            Environment.GetEnvironmentVariable("TELEMETRY_SAMPLING_RATIO"),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var ratio) && ratio is >= 0.0 and <= 1.0
                ? ratio
                : 0.2;

        // UseAzureMonitorExporter() instala internamente un RateLimitedSampler
        // (Azure.Monitor.OpenTelemetry.Exporter 1.8.1, TracesPerSecond default 5.0) que pisa
        // cualquier SetSampler previo (verificado en runtime): por eso el SetSampler va en un
        // segundo .WithTracing(...) DESPUES. No se envuelve en SamplerQueDescartaPollingDelDaemon
        // como en Projections: ControlHoras no corre ningun daemon.
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddSource("Wolverine")
                .AddSource("Marten")
                .AddSource("Npgsql")
                .AddSource("Bitakora.ControlAsistencia.ControlHoras.*"))
            .UseFunctionsWorkerDefaults()
            // MEF-ADR-0038 seccion 9: en su default (true) el exporter instala LogFilteringProcessor,
            // que descarta todo LogRecord emitido dentro de un span no muestreado; con sampling
            // fraccionario perderia en silencio los LogError que mira la alerta exception_spike.
            .UseAzureMonitorExporter(o => o.EnableTraceBasedLogsSampler = false)
            .WithTracing(tracing => tracing
                .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingRatio))))
            // UseAzureMonitorExporter() activa el pipeline de metricas sin opt-out por senal: ~76.000
            // metricas/dia de runtime que nadie consulta (CA-ADR-0009). El wildcard cubre tambien
            // instrumentos que aparezcan en un futuro upgrade del exporter.
            .WithMetrics(metrics => metrics.AddView(instrumentName: "*", MetricStreamConfiguration.Drop));

        // El View de arriba filtra mediciones, no evita que se construya el reader de metricas: ese
        // se construye de forma sincronica al resolver MeterProvider y su ctor exige una connection
        // string o lanza (decompilado de Azure.Monitor.OpenTelemetry.Exporter 1.8.1). Sin este
        // fallback, un arranque en frio con la Key Vault reference aun sin resolver tumbaria el
        // Function App. El valor es inerte: con todas las metricas dropeadas no hay nada que
        // exportar. Que nunca pise una connection string real lo sostiene
        // AgregarServicios*_ConservaLaConnectionStringReal.
        services.PostConfigure<AzureMonitorExporterOptions>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
                options.ConnectionString =
                    "InstrumentationKey=00000000-0000-0000-0000-000000000000;" +
                    "IngestionEndpoint=https://dummy.in.applicationinsights.azure.com/";
        });

        services.Configure<JsonSerializerOptions>(options =>
        {
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.PropertyNameCaseInsensitive = true;
        });

        services.AddScoped<IRequestValidator, RequestValidator>();
        services.AddValidatorsFromAssemblyContaining<IControlHorasAssemblyMarker>();

        services.AddScoped<IEventStoreReadinessProbe, EventStoreReadinessProbe>();

        return services;
    }
}
