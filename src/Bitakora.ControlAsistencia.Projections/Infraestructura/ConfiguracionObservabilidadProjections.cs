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
/// Seam de composicion de observabilidad del worker de proyecciones (issue #250). Program.cs
/// invoca este metodo -- no wirea OpenTelemetry inline -- igual que ya hace con
/// <see cref="ConfiguracionMartenProjections.ConfigurarEventos"/> (MEF-ADR-0029, hermano de este
/// seam). El worker corre sin ingress (MEF-ADR-0034 seccion 8), asi que la unica observabilidad
/// posible es trazas exportadas a Application Insights via OpenTelemetry: no hay endpoint HTTP que
/// las herramientas de diagnostico del marco puedan golpear.
/// </summary>
public static class ConfiguracionObservabilidadProjections
{
    // CA-3 / CA-ADR-0009 Capa 2 (control de costos): sampler head-based
    // ParentBasedSampler(TraceIdRatioBasedSampler(ratio)), ratio leido de esta variable de entorno.
    // Default 0.2 cuando la variable falta o es invalida. Para un diagnostico puntual se sube a 1.0
    // manualmente en el Container App y se baja despues -- no se sube el default en codigo.
    internal const string VariableRatioSampling = "TELEMETRY_SAMPLING_RATIO";
    internal const double RatioSamplingPorDefecto = 0.2;

    // Issue #263: nombre de servicio para el atributo de recurso OpenTelemetry `service.name`.
    // Debe coincidir EXACTAMENTE con el nombre del ensamblado del worker: PatronFuentePropia
    // (issue #250, abajo) ya nombra su ActivitySource con ese mismo string, asi que ambas
    // constantes derivan de una sola fuente de verdad -- fijado por guardrail en
    // ConfiguracionObservabilidadProjectionsTests. No lleva el ambiente: hay un Application Insights
    // por ambiente, asi que cloud_RoleName nunca tiene que desambiguar ambientes dentro del mismo
    // recurso (issue #263, punto 1 del refinamiento).
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

    // Issue #517 (CA-1, CA-ADR-0009): prefijo de la unica familia de metricas OTel que este worker
    // conserva. El resto se suprime -- ver SuprimirSalvoFamiliaGC mas abajo.
    internal const string PrefijoInstrumentosFamiliaGC = "dotnet.gc.";

    // Prefijos de categoria ILogger del daemon HotCold, que emite Information cada pocos segundos
    // 24/7 (min_replicas = 1). Son los que el filtro de abajo acota; el resto del worker conserva
    // su piso Information.
    private static readonly string[] CategoriasDelDaemon = ["JasperFx", "Marten"];

    public static IServiceCollection ConfigurarObservabilidad(this IServiceCollection services)
    {
        // Observabilidad: exporta las trazas del worker (Npgsql, Marten) a Application Insights via
        // OpenTelemetry. El worker no es ASP.NET Core ni Azure Functions (Microsoft.NET.Sdk.Worker,
        // Host.CreateApplicationBuilder), no recibe requests y corre sin ingress (MEF-ADR-0034
        // seccion 8): se usa el exporter, no el distro Azure.Monitor.OpenTelemetry.AspNetCore, ni
        // Microsoft.Azure.Functions.Worker.OpenTelemetry / UseFunctionsWorkerDefaults (no hay host de
        // Functions). Mismo argumento que ControlHoras/ComposicionServicios.cs, con mas fuerza aqui.
        // El exporter resuelve APPLICATIONINSIGHTS_CONNECTION_STRING del entorno por convencion
        // propia (no se lee ni se pasa a mano): la inyecta el Container App via Key Vault reference
        // (MEF-ADR-0025/CA-ADR-0026, issue #234).
        //
        // CA-ADR-0009 Capa 2 (control de costos): sampler head-based ParentBasedSampler +
        // TraceIdRatioBasedSampler, ratio configurable via TELEMETRY_SAMPLING_RATIO (default 0.2).
        // Este worker corre 24/7 con min_replicas = 1 (a diferencia de las Function Apps, que
        // escalan a cero), asi que puede generar volumen sostenido mayor -- el sampler debe estar
        // desde el dia uno. Para un diagnostico puntual se sube a 1.0 manualmente en el Container
        // App y se baja despues; el daily cap (Capa 3) y la alerta de spike (Capa 4) siguen activos.
        var samplingRatio = ResolverRatioDeSampling(
            Environment.GetEnvironmentVariable(VariableRatioSampling));

        // Issue #308 (hallazgo 1): UseAzureMonitorExporter() llama SetSampler internamente
        // (Azure.Monitor.OpenTelemetry.Exporter 1.8.1) con RateLimitedSampler porque
        // AzureMonitorExporterOptions.TracesPerSecond tiene default 5.0. Si el SetSampler del
        // proyecto se escribe ANTES de UseAzureMonitorExporter() (como estaba), ese SetSampler
        // interno lo pisa y el sampler configurado aqui nunca se instala -- verificado en runtime.
        // La correccion es de ORDEN: un segundo .WithTracing(...) DESPUES de
        // .UseAzureMonitorExporter() para que el SetSampler de este seam sea el que gane.
        services.AddOpenTelemetry()
            .ConfigureResource(ConfigurarRecurso)
            .WithTracing(tracing => tracing
                .AddSource("Marten")
                .AddSource("Npgsql")
                .AddSource(PatronFuentePropia))
            // MEF-ADR-0038 seccion 9: con su default `true`, el LogFilteringProcessor del exporter
            // descarta todo LogRecord emitido bajo un span no grabado -- incluidos los LogError que
            // el HighWaterAgent emite DENTRO del span de polling que el sampler de abajo dropea
            // (medido: 35/35 nunca llegaron a `exceptions`). NO revertir a UseAzureMonitorExporter()
            // sin argumentos ni volverlo opcion del consumidor: es mecanismo del marco.
            //
            // Efecto lateral del overload con callback: no registra DefaultAzureMonitorExporterOptions,
            // asi que APPLICATIONINSIGHTS_CONNECTION_STRING ya solo llega via IConfiguration -- la
            // puebla el proveedor de variables de entorno de Host.CreateApplicationBuilder. Un host
            // sin ese proveedor apagaria la exportacion completa en silencio.
            .UseAzureMonitorExporter(o => o.EnableTraceBasedLogsSampler = false)
            .WithTracing(tracing => tracing
                // Issue #308 (hallazgo 2): el daemon HotCold de Marten emite un span de polling sin
                // valor diagnostico cada 5s (marten.daemon.highwatermark) del que cuelga el 95% de
                // los spans Postgres del worker. Se envuelve el sampler de ratio para descartar esa
                // actividad puntual sin afectar al resto de la fuente Marten.
                .SetSampler(new SamplerQueDescartaPollingDelDaemon(
                    new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingRatio)))))
            // Issue #517 (CA-1, CA-ADR-0009): UseAzureMonitorExporter() activa el pipeline de
            // metricas (WithMetrics interno) aunque este seam solo pida trazas -- mismo hallazgo que
            // el hermano #515 (Function Apps; descompilado alli: OpenTelemetryBuilderExtensions.
            // UseAzureMonitorExporter llama WithMetrics(WithTracing(...)) por dentro). A diferencia
            // de #515 (procesos efimeros que escalan a cero: se dropea TODO con
            // AddView("*", MetricStreamConfiguration.Drop)), este worker corre 24/7
            // (min_replicas = 1, MEF-ADR-0034 seccion 8) hospedando el daemon de proyecciones: un
            // memory leak en una proyeccion es un riesgo real y silencioso que solo la serie de heap
            // (`dotnet.gc.*`) puede diagnosticar -- decision de producto del refinamiento,
            // CA-ADR-0009.
            //
            // Se usa el overload func-based (Func<Instrument, MetricStreamConfiguration>, XML doc de
            // OpenTelemetry.dll 1.16.0, MeterProviderBuilderExtensions.AddView) en vez de dos AddView
            // por patron de nombre ("dotnet.gc.*" + "*"): "las vistas se aplican en el orden en que
            // se agregan" y un instrumento que matchea VARIAS vistas produce un MetricStream POR CADA
            // una (fan-out, no "la primera gana") -- con dos vistas name-based, un instrumento GC
            // calzaria con ambas y el wildcard "*" seguiria generando un segundo stream dropeado para
            // el mismo instrumento. El overload func-based registra una UNICA vista que decide por
            // instrumento: esa misma doc XML documenta que una MetricStreamConfiguration invalida
            // (incluido null) devuelta por la funcion "causara que la vista se ignore para ese
            // instrumento, sin error en runtime" -- cae al agregado por defecto (se conserva), igual
            // que si ninguna vista existiera para el.
            .WithMetrics(metrics => metrics.AddView(SuprimirSalvoFamiliaGC));

        // Contrapeso obligado del AddView de arriba (issue #517, por analogia con el #515): el
        // reader de metricas que UseAzureMonitorExporter() instala se construye de forma SINCRONICA
        // al resolver MeterProvider y exige una connection string, o lanza
        // InvalidOperationException (verificado por decompilacion en el #515) -- sin importar que
        // las vistas dropeen todo despues. Sin este fallback, un arranque en frio con la Key Vault
        // reference de APPLICATIONINSIGHTS_CONNECTION_STRING aun sin resolver (MEF-ADR-0034
        // seccion 8) tumbaria el worker entero al resolver MeterProvider. El valor es inerte para los
        // instrumentos suprimidos (esa combinacion nunca abre una conexion real); si la variable de
        // entorno real esta presente, este PostConfigure no la pisa.
        services.PostConfigure<AzureMonitorExporterOptions>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
                options.ConnectionString =
                    "InstrumentationKey=00000000-0000-0000-0000-000000000000;" +
                    "IngestionEndpoint=https://dummy.in.applicationinsights.azure.com/";
        });

        // Contrapeso del flip de arriba, que por si solo dejaria pasar tambien los Information
        // rutinarios del daemon contra el daily cap (CA-ADR-0009 Capa 3). El filtro va sobre el
        // ILoggerProvider del exporter -- unico control de volumen de logs que queda del lado del
        // consumidor (MEF-ADR-0038 seccion 9) -- y no sobre el logging global: la consola del
        // Container App conserva Information, que fue la senal que permitio diagnosticar la
        // perdida. Error > Warning, asi que los LogError del daemon siguen exportandose.
        services.AddLogging(logging =>
        {
            foreach (var categoria in CategoriasDelDaemon)
                logging.AddFilter<OpenTelemetryLoggerProvider>(categoria, LogLevel.Warning);
        });

        return services;
    }

    // Extraido como metodo interno testeable en vez de leer Environment.GetEnvironmentVariable
    // inline (a diferencia de ControlHoras/ComposicionServicios.cs): permite verificar el parsing
    // del ratio (CA-3: default 0.2 ante ausencia/valor invalido/fuera de rango) sin mutar variables
    // de entorno de proceso en los tests, que corren en paralelo entre clases.
    internal static double ResolverRatioDeSampling(string? valorConfigurado) =>
        double.TryParse(
            valorConfigurado,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var ratio) && ratio is >= 0.0 and <= 1.0
            ? ratio
            : RatioSamplingPorDefecto;

    // Issue #263: fija el atributo de recurso OpenTelemetry `service.name`, la unica pieza que le
    // faltaba a este seam para que el worker deje de aparecer en Application Insights como
    // `unknown_service:dotnet` (cloud_RoleName cae a `service.name` cuando `service.namespace` no
    // esta seteado -- Microsoft Learn, "Configure Azure Monitor OpenTelemetry"). Extraido como
    // metodo interno -- igual que ResolverRatioDeSampling arriba -- para que el test pueda aislar la
    // precedencia frente al entorno sobre un ResourceBuilder propio, sin construir el contenedor.
    //
    // SIN serviceNamespace (cloud_RoleName no debe llevar prefijo) y SIN autogenerar
    // service.instance.id (autoGenerateServiceInstanceId: false): el default true reemplazaria el
    // hostname legible del contenedor por un GUID aleatorio distinto en cada arranque de revision;
    // el exporter de Azure Monitor deriva cloud_RoleInstance de ese atributo y cae al hostname en
    // su ausencia.
    //
    // Consecuencia conocida: fijar el nombre en codigo deja OTEL_SERVICE_NAME inerte
    // (ConfigureResource corre despues del ResourceBuilder.CreateDefault() que ya parsea esa
    // variable, y Resource.Merge le da precedencia a lo que se fusiona despues -- verificado en
    // ConfigurarRecurso_ConservaElServiceNameDelCodigo_CuandoOtelServiceNameApuntaAOtroValor).
    // Si alguna vez se necesita configurar el nombre por ambiente, el camino es un parametro nuevo
    // en este metodo, no la variable de entorno.
    internal static void ConfigurarRecurso(ResourceBuilder recurso) =>
        recurso.AddService(NombreServicio, autoGenerateServiceInstanceId: false);

    // Issue #517 (CA-1, CA-2): vista func-based para MeterProviderBuilder.AddView. Para un
    // instrumento de la familia GC devuelve null -- la doc XML de OpenTelemetry 1.16.0 documenta que
    // una MetricStreamConfiguration invalida hace que "la vista se ignore para ese instrumento", asi
    // que cae al agregado por defecto (se conserva sin intervencion). Para cualquier otro instrumento
    // devuelve Drop. Extraido como metodo internal -- mismo criterio que ResolverRatioDeSampling y
    // ConfigurarRecurso arriba -- para que el guardrail de composicion pueda ejercitarlo end-to-end
    // via el contenedor real sin duplicar la lista de nombres GC en el test.
    internal static MetricStreamConfiguration? SuprimirSalvoFamiliaGC(Instrument instrumento) =>
        instrumento.Name.StartsWith(PrefijoInstrumentosFamiliaGC, StringComparison.Ordinal)
            ? null
            : MetricStreamConfiguration.Drop;
}
