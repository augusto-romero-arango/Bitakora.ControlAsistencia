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
using Bitakora.ControlAsistencia.TenantResolver;
using FluentValidation;
using Marten;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Bitakora.ControlAsistencia.Sedes.Infraestructura;

// Program.cs y el test de composicion invocan este mismo metodo: un wiring roto no puede
// desincronizarse entre el host real y el guardrail de CI.
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
                // Apaga el polling de metricas de cola de Wolverine (timer de 5 s que llama
                // FetchCountsAsync): nadie las consume y CheckHealthAsync no depende de el. La
                // durabilidad (recovery, scheduled jobs, dead letters) sigue: DurabilityAgentEnabled y
                // Mode no se tocan.
                //
                // Va en este callback porque Cosmos.EventSourcing.CritterStack 2.3.1 lo corre primero
                // y despues reasigna Durability.Mode = Solo, que pisaria en silencio cualquier cambio
                // de Mode hecho aqui (no toca DurabilityMetricsEnabled; verificado descompilando). No
                // es contrato del paquete: lo sostiene el guardrail de ComposicionServiciosTests.
                options.Durability.DurabilityMetricsEnabled = false;

                options.HabilitarAzureServiceBusParaServerLess(serviceBusConnectionString);

                options.PublicarEventoServerless<SedeDeMarcacionResuelta>("sede-de-marcacion-resuelta");
            });

        services.AgregarMartenEventStore();
        // Identidad ambiente por AsyncLocal, poblada por TenantContextMiddleware en Program.cs. NO usar
        // AgregarTenantContextHibrido(): su ProxyTenantContext decide la rama en el constructor segun
        // HttpContext, que es null en el worker aislado, y toda request HTTP falla (CA-ADR-0032).
        services.AgregarTenantResolverControlAsistencia();
        services.AgregarWolverineCommandRouter();
        services.AgregarWolverineEventSender();
        services.AgregarWolverinePrivateEventRouter();
        services.AddScoped<ILectorSedesParaMarcacion, LectorReadSideSedes>();
        services.AddScoped<ILectorUbicacionDispositivo, LectorReadSideSedes>();

        services.ConfigureMarten(options =>
        {
            options.Events.AddEventTypes(IdentidadEventosSedes.TiposPersistidos);

            // La tabla de FichaSede la posee la proyeccion del worker, a la que Marten aplica
            // ProjectionDocumentPolicy: mt_version bigint, incondicional
            // (https://martendb.io/documents/concurrency, "Numeric Revisioned Documents"). Este store no
            // registra la proyeccion, asi que sin esta linea esperaria mt_version uuid sobre la misma
            // tabla: intenta "alter column" en cada request, Postgres lo rechaza con 42804 y los GET
            // dan 500 permanente (MEF-ADR-0034 seccion 6). El config-test del worker congela los mismos
            // literales: si cambias uno, cambia el otro.
            options.Schema.For<FichaSede>().UseNumericRevisions(true);

            // Misma razon para UbicacionDispositivo (LectorReadSideSedes): sin esta linea la reaccion
            // a la marcacion terminaria siempre en dead-letter.
            options.Schema.For<UbicacionDispositivo>().UseNumericRevisions(true);

            // Aqui dentro junto a AddEventTypes, nunca en un ConfigureMarten separado: un segundo
            // callback puede dejar el resolver sin efecto en silencio si cambia el orden. Sin esta
            // llamada, un VO con ctor privado pasaria los tests de serializacion y Marten reventaria
            // en runtime.
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

        // Sampling de CA-ADR-0009 Capa 2; default 0.2, igual que los otros dominios.
        var samplingRatio = double.TryParse(
            Environment.GetEnvironmentVariable("TELEMETRY_SAMPLING_RATIO"),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var ratio) && ratio is >= 0.0 and <= 1.0
                ? ratio
                : 0.2;

        // UseAzureMonitorExporter() instala su propio RateLimitedSampler (Exporter 1.8.1): el
        // SetSampler propio DEBE ir en un segundo WithTracing DESPUES de el o queda pisado
        // (verificado en runtime).
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddSource("Wolverine")
                .AddSource("Marten")
                .AddSource("Npgsql")
                .AddSource("Bitakora.ControlAsistencia.Sedes.*"))
            .UseFunctionsWorkerDefaults()
            // En true (default) el exporter descarta los LogRecord de spans no muestreados: con ratio
            // fraccionario se pierden en silencio los LogError que mira la alerta exception_spike
            // (MEF-ADR-0038 seccion 9).
            .UseAzureMonitorExporter(o => o.EnableTraceBasedLogsSampler = false)
            .WithTracing(tracing => tracing
                .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingRatio))))
            // UseAzureMonitorExporter() activa metricas sin opt-out por senal (~76.000/dia que nadie
            // consulta, CA-ADR-0009); el wildcard cubre tambien instrumentos de futuros upgrades.
            .WithMetrics(metrics => metrics.AddView(instrumentName: "*", MetricStreamConfiguration.Drop));

        // El View filtra mediciones, pero el reader de metricas del exporter se construye sincronico
        // al resolver MeterProvider y lanza sin connection string (decompilado de Exporter 1.8.1): sin
        // este fallback, un arranque en frio con la Key Vault reference sin resolver tumba el Function
        // App. El valor es inerte (todo se dropea); que no pise una connection string real lo garantiza
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
        services.AddValidatorsFromAssemblyContaining<ISedesAssemblyMarker>();

        services.AddScoped<IEventStoreReadinessProbe, EventStoreReadinessProbe>();

        return services;
    }
}
