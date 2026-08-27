using System.Globalization;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry;
using OpenTelemetry.Logs;
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

    // Issue #414 (CA-2): prefijos de categoria ILogger de las clases del daemon HotCold que hoy
    // exportan en Information cada pocos segundos, 24/7 (min_replicas = 1). El filtro sube su piso
    // de EXPORTACION a Warning para que dejen de presionar el daily cap (CA-ADR-0009 Capa 3) sin
    // perder los LogError que este issue rescata del modo de perdida binario descrito en el
    // encabezado del issue. "JasperFx" cubre JasperFx.Events.Daemon.HighWater.HighWaterAgent
    // (verificado por el planner contra consola real); "Marten" cubre el resto de categorias del
    // daemon bajo ese namespace. La consola del Container App (appsettings.json) no se toca: este
    // filtro se aplica sobre el ILoggerProvider que UseAzureMonitorExporter instala
    // (OpenTelemetry.Logs.OpenTelemetryLoggerProvider), no sobre el logging global del host.
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
            // Issue #414 (CA-1): EnableTraceBasedLogsSampler viene en `true` por defecto (1.8.1) e
            // instala un LogFilteringProcessor que descarta OnEnd(logRecord) salvo que
            // logRecord.SpanId == default || logRecord.TraceFlags == Recorded. Los LogError que
            // JasperFx.Events.Daemon.HighWater.HighWaterAgent emite DENTRO del span
            // marten.daemon.highwatermark (que SamplerQueDescartaPollingDelDaemon, issue #308,
            // dropea) caen del lado equivocado de esa condicion y nunca llegan a `exceptions`
            // (0% de ratio medido en el issue #412). El muestreo de logs se desacopla del de trazas
            // apagando este flag; el volumen resultante se controla con el filtro por proveedor de
            // CA-2, no con el sampler de trazas.
            .UseAzureMonitorExporter(o => o.EnableTraceBasedLogsSampler = false)
            .WithTracing(tracing => tracing
                // Issue #308 (hallazgo 2): el daemon HotCold de Marten emite un span de polling sin
                // valor diagnostico cada 5s (marten.daemon.highwatermark) del que cuelga el 95% de
                // los spans Postgres del worker. Se envuelve el sampler de ratio para descartar esa
                // actividad puntual sin afectar al resto de la fuente Marten.
                .SetSampler(new SamplerQueDescartaPollingDelDaemon(
                    new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingRatio)))));

        // Issue #414 (CA-2): el flag de CA-1 por si solo dejaria pasar TODOS los LogRecord del
        // daemon hacia el exporter, incluidos los `Information` rutinarios ("Executed updates for
        // Event range...") que emite cada pocos segundos con min_replicas = 1 -- presionando el
        // daily cap (CA-ADR-0009 Capa 3) en la direccion opuesta a la que este issue busca. Se sube
        // el piso de EXPORTACION (no el de la consola: appsettings.json queda intacto) de esas
        // categorias a Warning sobre el ILoggerProvider que UseAzureMonitorExporter instala
        // (OpenTelemetry.Logs.OpenTelemetryLoggerProvider) -- los LogError (Error > Warning) del
        // daemon siguen exportandose, los Information del ruido rutinario no. AddLogging es
        // aditivo: no reemplaza el AddLogging() ya invocado por el host del worker.
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
}
