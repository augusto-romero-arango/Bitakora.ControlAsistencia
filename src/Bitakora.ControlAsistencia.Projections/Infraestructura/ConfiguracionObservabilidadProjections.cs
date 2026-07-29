using System.Globalization;
using Azure.Monitor.OpenTelemetry.Exporter;
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

        services.AddOpenTelemetry()
            .ConfigureResource(ConfigurarRecurso)
            .WithTracing(tracing => tracing
                .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingRatio)))
                .AddSource("Marten")
                .AddSource("Npgsql")
                .AddSource(PatronFuentePropia))
            .UseAzureMonitorExporter();

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
