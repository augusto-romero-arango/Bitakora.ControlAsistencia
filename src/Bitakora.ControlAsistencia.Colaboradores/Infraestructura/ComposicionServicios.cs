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

// Program.cs y el test de composicion invocan este mismo metodo: un wiring roto no puede
// desincronizarse entre el host real y el guardrail de CI.
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
                // Apaga el polling de metricas de cola de Wolverine (PeriodicTimer de 5 s sobre
                // store.Admin.FetchCountsAsync()): nadie las consume y CheckHealthAsync no depende
                // de el. La durabilidad real (recovery, scheduled jobs, dead letters) no se toca.
                // Cosmos.EventSourcing.CritterStack 2.3.1 corre este callback PRIMERO y luego
                // reasigna Durability.Mode = Solo (pisaria cualquier cambio de Mode aqui); que
                // DurabilityMetricsEnabled sobreviva no es contrato del paquete, lo sostiene el
                // guardrail de ComposicionServiciosTests.
                options.Durability.DurabilityMetricsEnabled = false;

                options.HabilitarAzureServiceBusParaServerLess(serviceBusConnectionString);

                // Sin eventos de bus todavia: el primero se registra aqui con
                // options.PublicarEventoServerless<TEvento>("<topic>").
            });

        services.AgregarMartenEventStore();
        // Identidad ambiente por AsyncLocal, poblada por TenantContextMiddleware en Program.cs
        // (CA-ADR-0032). NO usar AgregarTenantContextHibrido(): su ProxyTenantContext decide la rama
        // en el constructor segun IHttpContextAccessor.HttpContext, que es null en el worker aislado.
        services.AgregarTenantResolverControlAsistencia();
        services.AgregarWolverineCommandRouter();
        services.AgregarWolverineEventSender();
        // AgregarWolverinePrivateEventRouter() no se registra: el dominio no consume eventos
        // privados. Se agrega con su primer FunctionEndpoint de Service Bus.

        services.ConfigureMarten(options =>
        {
            options.Events.AddEventTypes(IdentidadEventosColaboradores.TiposPersistidos);

            // Cada vista materializada que este store consulta declara aqui la forma de mt_version
            // que el worker impone al escribirla. Marten aplica ProjectionDocumentPolicy (mt_version
            // bigint) a todo target de una proyeccion registrada, sin opt-in
            // (https://martendb.io/documents/concurrency, "Numeric Revisioned Documents"); este store
            // no registra la proyeccion (vive en el worker, CA-ADR-0029), asi que sin esta linea
            // esperaria mt_version uuid sobre la MISMA tabla: con AutoCreate CreateOrUpdate intenta
            // el alter en CADA request, Postgres lo rechaza (42804) y los GET dan 500 permanente.
            // Por documento y no via Policies, para no tocar otros documentos. Si cambias esto,
            // cambia tambien el config-test gemelo del worker.
            options.Schema.For<FichaColaborador>().UseNumericRevisions(true);

            options.Schema.For<CategoriaDeEtiquetas>().UseNumericRevisions(true);
            options.Schema.For<DirectorioColaborador>().UseNumericRevisions(true);

            // AQUI DENTRO, nunca en un ConfigureMarten separado: un segundo callback puede dejar el
            // resolver custom sin efecto en silencio si cambia el orden (lo verifica
            // ComposicionServiciosTests sobre el store real).
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

        var samplingRatio = double.TryParse(
            Environment.GetEnvironmentVariable("TELEMETRY_SAMPLING_RATIO"),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var ratio) && ratio is >= 0.0 and <= 1.0
                ? ratio
                : 0.2;

        // UseAzureMonitorExporter() (Azure.Monitor.OpenTelemetry.Exporter 1.8.1) instala su propio
        // RateLimitedSampler: el SetSampler propio DEBE ir en un segundo .WithTracing(...) DESPUES
        // o queda pisado (verificado en runtime sobre ControlHoras).
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddSource("Wolverine")
                .AddSource("Marten")
                .AddSource("Npgsql")
                .AddSource("Bitakora.ControlAsistencia.Colaboradores.*"))
            .UseFunctionsWorkerDefaults()
            // En true (default) el exporter descarta todo LogRecord emitido dentro de un span no
            // muestreado: con sampling fraccionario se perderian en silencio los LogError que mira
            // la alerta exception_spike (MEF-ADR-0038 seccion 9).
            .UseAzureMonitorExporter(o => o.EnableTraceBasedLogsSampler = false)
            .WithTracing(tracing => tracing
                .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingRatio))))
            // UseAzureMonitorExporter() activa las metricas sin opt-out por senal (~76.000/dia que
            // nadie consulta, CA-ADR-0009). El wildcard cubre tambien instrumentos de futuros
            // upgrades del exporter.
            .WithMetrics(metrics => metrics.AddView(instrumentName: "*", MetricStreamConfiguration.Drop));

        // El View no evita que se construya el reader de metricas: se construye SINCRONICO al
        // resolver MeterProvider y su ctor lanza sin connection string (decompilado del exporter
        // 1.8.1). Sin este fallback, un arranque en frio con la Key Vault reference sin resolver
        // tumba el Function App. El valor es inerte (todo se dropea); que no pise una connection
        // string real lo fija AgregarServicios*_ConservaLaConnectionStringReal.
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
        services.AddValidatorsFromAssemblyContaining<IColaboradoresAssemblyMarker>();

        services.AddScoped<IEventStoreReadinessProbe, EventStoreReadinessProbe>();

        return services;
    }
}
