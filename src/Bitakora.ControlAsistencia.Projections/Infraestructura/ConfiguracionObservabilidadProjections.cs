using System.Diagnostics.Metrics;
using System.Globalization;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Bitakora.ControlAsistencia.Projections.Infraestructura;

/// <summary>
/// Seam de observabilidad del worker de proyecciones. El worker corre sin ingress (MEF-ADR-0034
/// seccion 8): su unica observabilidad es la telemetria exportada a Application Insights.
/// </summary>
public static class ConfiguracionObservabilidadProjections
{
    // CA-ADR-0009 Capa 2. Default 0.2 si falta o es invalida. Para un diagnostico puntual se sube a
    // 1.0 en el Container App y se baja despues; no se sube el default en codigo.
    internal const string VariableRatioSampling = "TELEMETRY_SAMPLING_RATIO";
    internal const double RatioSamplingPorDefecto = 0.2;

    // `service.name`. Debe coincidir EXACTAMENTE con el nombre del ensamblado del worker, del que
    // deriva PatronFuentePropia (guardrail en ConfiguracionObservabilidadProjectionsTests). Sin
    // ambiente: hay un Application Insights por ambiente.
    internal const string NombreServicio = "Bitakora.ControlAsistencia.Projections";

    // CA-2: el source del propio ensamblado. El wildcard va SIN punto antes del asterisco --
    // divergencia deliberada frente a ControlHoras ("...ControlHoras.*"), verificada
    // empiricamente contra OpenTelemetry 1.16.0: el patron "X.*" se ancla como ^X\..*$ y NO
    // captura una ActivitySource nombrada exactamente "X", mientras "X*" captura tanto "X" como
    // "X.Hija". Como lo idiomatico al instrumentar es nombrar la fuente con el nombre del
    // ensamblado (`Assembly.GetName().Name`), con el punto los spans del propio worker se
    // descartarian EN SILENCIO -- el mismo tipo de wiring a medio terminar que este issue cierra.
    // Derivado de NombreServicio (concatenacion de constantes, resuelta en tiempo de compilacion)
    // para que ambas constantes no puedan divergir -- fijado por guardrail en
    // ConfiguracionObservabilidadProjectionsTests.
    internal const string PatronFuentePropia = NombreServicio + "*";

    private const string PrefijoInstrumentosFamiliaGC = "dotnet.gc.";

    // Prefijos de categoria ILogger del daemon HotCold, que emite Information cada pocos segundos
    // 24/7 (min_replicas = 1). Son los que el filtro de abajo acota; el resto del worker conserva
    // su piso Information.
    private static readonly string[] CategoriasDelDaemon = ["JasperFx", "Marten"];

    public static IServiceCollection ConfigurarObservabilidad(this IServiceCollection services)
    {
        // El worker no es ASP.NET Core ni Azure Functions: se usa el exporter, no el distro AspNetCore
        // ni UseFunctionsWorkerDefaults. APPLICATIONINSIGHTS_CONNECTION_STRING la inyecta el Container
        // App via Key Vault reference.
        //
        // A diferencia de las Function Apps, corre 24/7 con min_replicas = 1: el sampler de ratio es
        // obligatorio desde el dia uno.
        var samplingRatio = ResolverRatioDeSampling(
            Environment.GetEnvironmentVariable(VariableRatioSampling));

        // UseAzureMonitorExporter() instala su propio RateLimitedSampler (Exporter 1.8.1): el
        // SetSampler propio DEBE ir en un segundo WithTracing DESPUES de el o queda pisado
        // (verificado en runtime).
        services.AddOpenTelemetry()
            .ConfigureResource(ConfigurarRecurso)
            .WithTracing(tracing => tracing
                .AddSource("Marten")
                .AddSource("Npgsql")
                .AddSource(PatronFuentePropia))
            // MEF-ADR-0038 seccion 9: en `true` (default) el exporter descarta todo LogRecord emitido
            // bajo un span no grabado, incluidos los LogError del HighWaterAgent dentro del span de
            // polling que el sampler dropea (medido: 35/35 perdidos). NO revertir a
            // UseAzureMonitorExporter() sin argumentos ni volverlo opcion del consumidor.
            .UseAzureMonitorExporter(o => o.EnableTraceBasedLogsSampler = false)
            .WithTracing(tracing => tracing
                .SetSampler(new SamplerQueDescartaPollingDelDaemon(
                    new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingRatio)))))
            // UseAzureMonitorExporter() activa tambien metricas que nadie consume (CA-ADR-0009). No se
            // dropea TODO como en las Function Apps: en un worker 24/7 la serie de heap es la unica que
            // anticipa un memory leak del daemon.
            //
            // Una UNICA vista func-based: un instrumento que casa con varias vistas produce un
            // MetricStream por cada una, asi que AddView("dotnet.gc.*", ...) + AddView("*", Drop)
            // dropearia tambien la familia GC.
            .WithMetrics(metrics => metrics.AddView(SuprimirSalvoFamiliaGC));

        // El AddView filtra mediciones, pero el reader de metricas del exporter se construye sincronico
        // al resolver MeterProvider y lanza sin connection string (decompilado de Exporter 1.8.1): sin
        // este fallback, un arranque en frio con la Key Vault reference sin resolver tumba el worker.
        // Que no pise una connection string real lo garantiza
        // ConfigurarObservabilidad_ResuelveLaConnectionStringDelEntorno_EnLasOpcionesDelExporter.
        services.PostConfigure<AzureMonitorExporterOptions>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
                options.ConnectionString =
                    "InstrumentationKey=00000000-0000-0000-0000-000000000000;" +
                    "IngestionEndpoint=https://dummy.in.applicationinsights.azure.com/";
        });

        // Contrapeso de EnableTraceBasedLogsSampler = false, que dejaria pasar los Information
        // rutinarios del daemon contra el daily cap. Va sobre el provider del exporter y no sobre el
        // logging global: la consola del Container App conserva Information para diagnosticar.
        services.AddLogging(logging =>
        {
            foreach (var categoria in CategoriasDelDaemon)
                logging.AddFilter<OpenTelemetryLoggerProvider>(categoria, LogLevel.Warning);
        });

        return services;
    }

    // Recibe el valor en vez de leer el entorno: los tests corren en paralelo y no deben mutar
    // variables de entorno del proceso.
    internal static double ResolverRatioDeSampling(string? valorConfigurado) =>
        double.TryParse(
            valorConfigurado,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var ratio) && ratio is >= 0.0 and <= 1.0
            ? ratio
            : RatioSamplingPorDefecto;

    // Sin `service.name` el worker aparece como `unknown_service:dotnet`: cloud_RoleName cae a
    // service.name cuando no hay service.namespace (Microsoft Learn, "Configure Azure Monitor
    // OpenTelemetry"). Sin serviceNamespace, para que cloud_RoleName no lleve prefijo, y sin
    // autogenerar service.instance.id: un GUID por arranque reemplazaria el hostname que el exporter
    // usa como cloud_RoleInstance.
    //
    // Deja OTEL_SERVICE_NAME inerte: Resource.Merge da precedencia a lo fusionado despues (verificado
    // en ConfigurarRecurso_ConservaElServiceNameDelCodigo_CuandoOtelServiceNameApuntaAOtroValor). Para
    // nombrarlo por ambiente, agrega un parametro aqui, no la variable de entorno.
    internal static void ConfigurarRecurso(ResourceBuilder recurso) =>
        recurso.AddService(NombreServicio, autoGenerateServiceInstanceId: false);

    // El null NO es "sin configuracion": devolver una MetricStreamConfiguration invalida hace que
    // AddView IGNORE la vista para ese instrumento, que asi cae al agregado por defecto y sobrevive
    // (xmldoc de OpenTelemetry 1.16.0, MeterProviderBuilderExtensions.AddView). Es el mecanismo por
    // el que se conserva la familia GC.
    private static MetricStreamConfiguration? SuprimirSalvoFamiliaGC(Instrument instrumento) =>
        instrumento.Name.StartsWith(PrefijoInstrumentosFamiliaGC, StringComparison.Ordinal)
            ? null
            : MetricStreamConfiguration.Drop;
}
