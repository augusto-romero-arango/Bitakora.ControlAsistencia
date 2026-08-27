// Issue #250: instrumentar el worker de proyecciones con Application Insights.
// Issue #263: fijar el recurso `service.name` para que el worker deje de aparecer en Application
// Insights como `unknown_service:dotnet`. Guardrails al final de esta misma clase (CA-1 a CA-4 de
// ese issue) -- misma familia de tests, no una clase nueva. Ojo al leer los "CA-N" de este archivo:
// los de la mitad de arriba son del issue #250 y los de la ultima seccion, del #263.
//
// El worker corre sin ingress (MEF-ADR-0034 seccion 8): no hay endpoint HTTP que golpear para
// diagnosticar el daemon HotCold, asi que la observabilidad depende exclusivamente de trazas
// OpenTelemetry exportadas a Application Insights. Este test de composicion es hermano del que
// exige MEF-ADR-0029: construye el mismo IServiceCollection que Program.cs compone (via el seam
// ConfiguracionObservabilidadProjections.ConfigurarObservabilidad, CA-4) y verifica que el
// TracerProvider se resuelve del contenedor (CA-2, CA-5), sin necesidad de Postgres real ni de
// un Container App desplegado.
//
// El parsing del ratio de sampling (CA-3, CA-ADR-0009 Capa 2) se extrajo a un metodo interno
// testeable (ResolverRatioDeSampling) en vez de leerse inline de Environment.GetEnvironmentVariable
// dentro del seam: permite verificar el default (0.2) ante ausencia/valor invalido/fuera de rango
// sin depender del entorno del proceso de test.
//
// Limites conocidos de esta clase (documentados, no huecos accidentales):
//
// 1. Que Program.cs invoque el seam (CA-4 del issue #250) no se verifica: Program.cs son
//    top-level statements y el worker no tiene equivalente a WebApplicationFactory (limite que
//    MEF-ADR-0029 ya reconoce). Lo mas cerca que llega esta clase es componer juntos los dos seams
//    del worker (ConfigurarEventos + ConfigurarObservabilidad), que es lo que Program.cs encadena;
//    el enlace literal queda cubierto por revision de codigo y por el arranque real post-deploy
//    (MEF-ADR-0013).
//
// Issue #308: el limite "el sampler compuesto vive dentro de TracerProviderSdk, que OpenTelemetry
// no expone publicamente" que este archivo declaraba antes resulto SUPERABLE -- ver el helper
// SamplerEfectivo.De (reflection sobre la propiedad interna Sampler) y los tests
// "ConfigurarObservabilidad_*ElSampler*" mas abajo. Es determinista (compara tipos y lee
// Description, que es publica), no probabilistico: no hace falta muestrear actividades reales
// contra un ratio fraccionario. La revision de codigo
// NO detecto el hallazgo 1 del issue #308 (UseAzureMonitorExporter pisa el sampler por dentro)
// precisamente porque el codigo visible de este seam era correcto -- el defecto estaba en la
// implementacion interna del exporter. De ahi que el guardrail deje de asumir que ese limite es
// permanente.
using System.Diagnostics;
using System.Globalization;
using AwesomeAssertions;
using Azure.Monitor.OpenTelemetry.Exporter;
using Bitakora.ControlAsistencia.Projections.Infraestructura;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Bitakora.ControlAsistencia.Projections.Tests.Infraestructura;

public class ConfiguracionObservabilidadProjectionsTests
{
    private const string VariableConnectionString = "APPLICATIONINSIGHTS_CONNECTION_STRING";

    private const string ConnectionStringAppInsightsDummy =
        "InstrumentationKey=00000000-0000-0000-0000-000000000000;" +
        "IngestionEndpoint=https://dummy.in.applicationinsights.azure.com/";

    private const string MartenConnectionStringDummy = "Host=localhost;Database=dummy";

    // Atributos de recurso de la seccion "service.name" (issue #263). Nombres literales a proposito:
    // son la clave de la convencion semantica de OpenTelemetry de la que el exporter de Azure Monitor
    // deriva cloud_RoleName/cloud_RoleInstance -- si el SDK los renombrara, el test debe fallar.
    private const string AtributoServiceName = "service.name";
    private const string AtributoServiceNamespace = "service.namespace";
    private const string AtributoServiceInstanceId = "service.instance.id";
    private const string VariableOtelServiceName = "OTEL_SERVICE_NAME";

    private static ServiceProvider ComponerServiceProvider(
        Action<IServiceCollection>? seamsAdicionales = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.ConfigurarObservabilidad();
        seamsAdicionales?.Invoke(services);

        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    // Fija el valor de la variable de entorno mientras corre la accion y lo restaura despues
    // (null la elimina). Sin esto, el escenario "connection string ausente" no seria determinista:
    // dependeria de que el entorno del proceso de test no la tenga seteada, y CI si podria tenerla.
    private static void ConVariableDeEntorno(string nombre, string? valor, Action accion)
    {
        var original = Environment.GetEnvironmentVariable(nombre);
        Environment.SetEnvironmentVariable(nombre, valor);
        try
        {
            accion();
        }
        finally
        {
            Environment.SetEnvironmentVariable(nombre, original);
        }
    }

    // --- Composicion del seam (CA-2, CA-4, CA-5) ---

    [Fact]
    public void ConfigurarObservabilidad_ComponeElGrafoCompleto_CuandoSeValidaAlConstruir()
    {
        var act = () => ComponerServiceProvider().Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public void ConfigurarObservabilidad_ResuelveTracerProviderDelContenedor_CuandoLaConnectionStringEstaAusente()
    {
        // Escenario de arranque local y de una revision cuya Key Vault reference aun no resolvio
        // (MEF-ADR-0034 seccion 8, secreto sembrado despues del apply): el exporter no debe tumbar
        // la composicion del worker por falta de connection string -- solo falla al exportar.
        ConVariableDeEntorno(VariableConnectionString, null, () =>
        {
            using var provider = ComponerServiceProvider();

            var tracerProvider = provider.GetService<TracerProvider>();

            tracerProvider.Should().NotBeNull();
        });
    }

    [Fact]
    public void ConfigurarObservabilidad_ResuelveTracerProviderDelContenedor_CuandoLaConnectionStringEstaPresente()
    {
        // Replica el escenario real (MEF-ADR-0025/CA-ADR-0026): el Container App inyecta esta
        // variable via Key Vault reference; el seam no la lee ni la pasa a mano -- la resuelve el
        // exporter por convencion propia.
        ConVariableDeEntorno(VariableConnectionString, ConnectionStringAppInsightsDummy, () =>
        {
            using var provider = ComponerServiceProvider();

            var tracerProvider = provider.GetService<TracerProvider>();

            tracerProvider.Should().NotBeNull();
        });
    }

    // Los dos seams del worker se registran sobre el mismo IServiceCollection, en el mismo orden en
    // que Program.cs los encadena. Sin esta guarda, cada seam queda verde por separado y un choque
    // entre ambos (registro duplicado, dependencia que uno espera del otro) solo aparece al
    // arrancar el proceso real. Sigue sin necesitar Postgres: Marten 7+ no abre conexion durante el
    // bootstrapping del IHost.
    [Fact]
    public void ConfigurarObservabilidad_ResuelveTracerProviderDelContenedor_CuandoSeComponeJuntoAlSeamDeEventos()
    {
        using var provider = ComponerServiceProvider(
            services => services.ConfigurarEventos(MartenConnectionStringDummy));

        provider.GetService<TracerProvider>().Should().NotBeNull();
        provider.GetService<IProgramacionProjectionStore>().Should().NotBeNull();
        provider.GetService<IControlHorasProjectionStore>().Should().NotBeNull();
    }

    // --- Sampler efectivo del TracerProvider (issue #308 CA-1, CA-3) ---
    //
    // Hallazgo 1 del issue: UseAzureMonitorExporter llama SetSampler internamente
    // (Azure.Monitor.OpenTelemetry.Exporter 1.8.1) con RateLimitedSampler cuando
    // AzureMonitorExporterOptions.TracesPerSecond tiene valor (default 5.0, SIEMPRE lo tiene salvo
    // que el codigo lo ponga en null explicitamente). Escrito en el orden actual de este seam
    // (SetSampler ANTES de UseAzureMonitorExporter), ese SetSampler interno pisa al que el proyecto
    // configura -- el ParentBasedSampler{TraceIdRatioBasedSampler{ratio}} que CA-ADR-0009 Capa 2
    // describe nunca llega a instalarse. La correccion es de ORDEN (un segundo .WithTracing(...)
    // despues de .UseAzureMonitorExporter()), no de contenido: por eso el guardrail verifica el
    // sampler EFECTIVO del contenedor, no que el codigo "llame a SetSampler" (eso ya lo hacia el
    // seam roto).
    [Fact]
    public void ConfigurarObservabilidad_ResuelveElSamplerConfiguradoPorElProyecto_EnVezDeRateLimitedSampler()
    {
        using var provider = ComponerServiceProvider();
        var tracerProvider = provider.GetRequiredService<TracerProvider>();

        var samplerEfectivo = SamplerEfectivo.De(tracerProvider);

        // Oraculo por nombre completo, no por referencia al tipo (es internal en otro ensamblado:
        // Azure.Monitor.OpenTelemetry.Exporter.Internals.RateLimitedSampler no se puede nombrar
        // desde este proyecto). Verificado en runtime (issue #308) que este es exactamente el tipo
        // que gana hoy con el wiring actual.
        samplerEfectivo.GetType().FullName.Should().NotBe(
            "Azure.Monitor.OpenTelemetry.Exporter.Internals.RateLimitedSampler");
        samplerEfectivo.Should().BeOfType<SamplerQueDescartaPollingDelDaemon>();
    }

    // CA-3: complementa (no reemplaza) ResolverRatioDeSampling_* de mas abajo -- aquellos verifican
    // solo el PARSING del valor de entorno; este verifica que el ratio resuelto efectivamente llega
    // al sampler compuesto que el contenedor resuelve. Antes del issue #308 esto se declaraba como
    // limite conocido (no observable sin acceder a TracerProviderSdk); dejo de serlo con
    // SamplerEfectivo.De + Sampler.Description (propiedad publica, que el wrapper compone con la del
    // sampler de ratio que envuelve:
    // "SamplerQueDescartaPollingDelDaemon{ParentBased{TraceIdRatioBasedSampler{F6}}}").
    [Fact]
    public void ConfigurarObservabilidad_PropagaElRatioDeSamplingConfigurado_AlSamplerEfectivo()
    {
        ConVariableDeEntorno(ConfiguracionObservabilidadProjections.VariableRatioSampling, "0.5", () =>
        {
            using var provider = ComponerServiceProvider();
            var tracerProvider = provider.GetRequiredService<TracerProvider>();

            var samplerEfectivo = SamplerEfectivo.De(tracerProvider);

            samplerEfectivo.Description.Should().Contain(FormatearRatio(0.5));
        });
    }

    // La otra mitad de CA-3, y la que mas importa hoy: TELEMETRY_SAMPLING_RATIO no esta puesta en
    // ningun recurso desplegado (medido en el issue #308), asi que el camino que efectivamente corre
    // en dev es el del DEFAULT. Sin este guardrail, el sampler efectivo podria quedar con un ratio
    // distinto al que RatioSamplingPorDefecto declara y solo se veria como un volumen de ingestion
    // inesperado -- el mismo modo de falla silenciosa que este issue corrige.
    [Fact]
    public void ConfigurarObservabilidad_PropagaElRatioPorDefecto_AlSamplerEfectivo_CuandoLaVariableEstaAusente()
    {
        ConVariableDeEntorno(ConfiguracionObservabilidadProjections.VariableRatioSampling, null, () =>
        {
            using var provider = ComponerServiceProvider();
            var tracerProvider = provider.GetRequiredService<TracerProvider>();

            var samplerEfectivo = SamplerEfectivo.De(tracerProvider);

            samplerEfectivo.Description.Should().Contain(
                FormatearRatio(ConfiguracionObservabilidadProjections.RatioSamplingPorDefecto));
        });
    }

    // TraceIdRatioBasedSampler embebe el ratio en su Description con formato F6 invariante
    // ("TraceIdRatioBasedSampler{0.200000}"), verificado contra OpenTelemetry 1.16.0.
    private static string FormatearRatio(double ratio) =>
        ratio.ToString("F6", CultureInfo.InvariantCulture);

    // El patron de la fuente propia (CA-2) va sin punto antes del asterisco a proposito. Verificado
    // contra OpenTelemetry 1.16.0: "X.*" se ancla como ^X\..*$ y descarta una ActivitySource
    // nombrada exactamente "X", que es como se nombra al instrumentar con el nombre del ensamblado.
    // Esta guarda evita que alguien "restaure la paridad" con ControlHoras ("...ControlHoras.*") y
    // deje los spans del worker sin capturar EN SILENCIO. Construye su propio TracerProvider en vez
    // de usar el del contenedor porque ese lleva el sampler de ratio 0.2 (CA-3), que volveria la
    // asercion probabilistica; aca el sampler por defecto la vuelve determinista.
    [Fact]
    public void PatronFuentePropia_CapturaLaActividad_CuandoLaFuenteSeLlamaComoElEnsambladoDelWorker()
    {
        var nombreDelEnsamblado = typeof(ConfiguracionObservabilidadProjections).Assembly.GetName().Name!;
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ConfiguracionObservabilidadProjections.PatronFuentePropia)
            .Build();
        using var fuente = new ActivitySource(nombreDelEnsamblado);

        using var actividad = fuente.StartActivity("operacion-de-prueba");

        actividad.Should().NotBeNull();
    }

    // --- Parsing del ratio de sampling (CA-3, CA-6, CA-ADR-0009 Capa 2) ---

    [Fact]
    public void ResolverRatioDeSampling_DevuelveElRatioPorDefecto_CuandoValorEsNull()
    {
        var ratio = ConfiguracionObservabilidadProjections.ResolverRatioDeSampling(null);

        ratio.Should().Be(ConfiguracionObservabilidadProjections.RatioSamplingPorDefecto);
    }

    [Fact]
    public void ResolverRatioDeSampling_DevuelveElRatioPorDefecto_CuandoValorNoEsNumerico()
    {
        var ratio = ConfiguracionObservabilidadProjections.ResolverRatioDeSampling("no-es-un-numero");

        ratio.Should().Be(ConfiguracionObservabilidadProjections.RatioSamplingPorDefecto);
    }

    [Fact]
    public void ResolverRatioDeSampling_DevuelveElRatioPorDefecto_CuandoValorEstaPorEncimaDeUno()
    {
        var ratio = ConfiguracionObservabilidadProjections.ResolverRatioDeSampling("1.5");

        ratio.Should().Be(ConfiguracionObservabilidadProjections.RatioSamplingPorDefecto);
    }

    [Fact]
    public void ResolverRatioDeSampling_DevuelveElRatioPorDefecto_CuandoValorEsNegativo()
    {
        var ratio = ConfiguracionObservabilidadProjections.ResolverRatioDeSampling("-0.3");

        ratio.Should().Be(ConfiguracionObservabilidadProjections.RatioSamplingPorDefecto);
    }

    [Fact]
    public void ResolverRatioDeSampling_DevuelveElValorConfigurado_CuandoEsValido()
    {
        var ratio = ConfiguracionObservabilidadProjections.ResolverRatioDeSampling("0.5");

        ratio.Should().Be(0.5);
    }

    // --- Recurso de OpenTelemetry: service.name (issue #263, CA-1 a CA-4) ---
    //
    // Dos niveles, deliberadamente:
    //
    // a) ConfigurarObservabilidad_* compone el seam completo y lee el Resource que el TracerProvider
    //    del contenedor termino usando (ProviderExtensions.GetResource, API publica de OpenTelemetry
    //    1.16.0 -- la misma via por la que el exporter de Azure Monitor deriva cloud_RoleName). Es lo
    //    que cubre CA-1 de punta a punta: que el enganche .ConfigureResource(...) sobre el
    //    IOpenTelemetryBuilder efectivamente alcance la senal de trazas, y no solo que
    //    ConfigurarRecurso haga lo correcto si alguien lo llama.
    // b) ConfigurarRecurso_* invoca el metodo sobre un ResourceBuilder propio, para aislar la
    //    precedencia frente al entorno (CA-4) sin construir el contenedor. CreateDefault() (no
    //    CreateEmpty()) es deliberado: es lo que el SDK usa en produccion e incluye el fallback
    //    `unknown_service:` y el detector de OTEL_SERVICE_NAME/OTEL_RESOURCE_ATTRIBUTES (verificado
    //    contra el XML doc de OpenTelemetry 1.16.0) -- exactamente lo que CA-4 necesita para
    //    demostrar que el nombre del codigo gana sobre ambos.

    [Fact]
    public void ConfigurarObservabilidad_FijaElServiceNameDelWorkerEnElRecursoDelTracerProvider()
    {
        using var provider = ComponerServiceProvider();

        var atributos = provider.GetRequiredService<TracerProvider>()
            .GetResource().Attributes.ToDictionary(a => a.Key, a => a.Value);

        atributos.Should().ContainKey(AtributoServiceName)
            .WhoseValue.Should().Be(ConfiguracionObservabilidadProjections.NombreServicio);
        atributos.Should().NotContainKey(AtributoServiceNamespace);
        atributos.Should().NotContainKey(AtributoServiceInstanceId);
    }

    [Fact]
    public void ConfigurarRecurso_FijaServiceNameSinNamespaceNiInstanceId()
    {
        var recurso = ResourceBuilder.CreateDefault();

        ConfiguracionObservabilidadProjections.ConfigurarRecurso(recurso);
        var atributos = recurso.Build().Attributes.ToDictionary(a => a.Key, a => a.Value);

        atributos.Should().ContainKey(AtributoServiceName)
            .WhoseValue.Should().Be(ConfiguracionObservabilidadProjections.NombreServicio);
        atributos.Should().NotContainKey(AtributoServiceNamespace);
        atributos.Should().NotContainKey(AtributoServiceInstanceId);
    }

    // CA-3: el nombre de servicio no puede divergir del nombre del ensamblado del worker -- es el
    // mismo criterio (idiomatico) por el que PatronFuentePropia (issue #250, arriba) nombra su
    // ActivitySource, y de ahi que una sola constante alimente a los dos.
    [Fact]
    public void NombreServicio_CoincideConElNombreDelEnsambladoDelWorker()
    {
        var nombreDelEnsamblado = typeof(ConfiguracionObservabilidadProjections).Assembly.GetName().Name!;

        ConfiguracionObservabilidadProjections.NombreServicio.Should().Be(nombreDelEnsamblado);
    }

    // CA-3: PatronFuentePropia se deriva hoy de NombreServicio (`NombreServicio + "*"`), asi que
    // mientras esa derivacion siga en pie esta guarda no puede fallar -- y eso es justamente lo que
    // fija: impide que alguien vuelva a escribir el literal a mano en una de las dos constantes y las
    // deje divergir en silencio (el modo de falla que este issue corrige). Es el guardrail de la
    // derivacion, no una asercion sobre el valor: ese lo cubre el test anterior contra el ensamblado.
    [Fact]
    public void PatronFuentePropia_CoincideConNombreServicioMasAsterisco()
    {
        ConfiguracionObservabilidadProjections.PatronFuentePropia.Should()
            .Be(ConfiguracionObservabilidadProjections.NombreServicio + "*");
    }

    // CA-4: precedencia sobre OTEL_SERVICE_NAME. ResourceBuilder.CreateDefault() ya parsea esa
    // variable (XML doc de OpenTelemetry 1.16.0); si ConfigurarRecurso corriera ANTES de que el
    // detector de entorno se aplicara, o si AddService no sobrescribiera el atributo, este test lo
    // detectaria. Reusa el helper ConVariableDeEntorno de arriba (seguro con tests en paralelo).
    [Fact]
    public void ConfigurarRecurso_ConservaElServiceNameDelCodigo_CuandoOtelServiceNameApuntaAOtroValor()
    {
        ConVariableDeEntorno(VariableOtelServiceName, "un-nombre-distinto-desde-el-entorno", () =>
        {
            var recurso = ResourceBuilder.CreateDefault();

            ConfiguracionObservabilidadProjections.ConfigurarRecurso(recurso);
            var atributos = recurso.Build().Attributes.ToDictionary(a => a.Key, a => a.Value);

            atributos.Should().ContainKey(AtributoServiceName)
                .WhoseValue.Should().Be(ConfiguracionObservabilidadProjections.NombreServicio);
        });
    }

    // --- Desacople del muestreo de logs del de trazas (MEF-ADR-0038 seccion 9) ---

    // Restriccion: sin el flip, el LogFilteringProcessor que instala el exporter descarta todo
    // LogRecord emitido bajo un span no grabado -- incluidos los LogError del HighWaterAgent, que
    // caen dentro del span de polling que SamplerQueDescartaPollingDelDaemon dropea (medido: 35/35
    // nunca llegaron a `exceptions`). Se lee el valor RESUELTO del contenedor, no el texto del seam.
    [Fact]
    public void ConfigurarObservabilidad_DeshabilitaElSamplerDeLogsBasadoEnTrazas()
    {
        using var provider = ComponerServiceProvider();

        var opciones = provider.GetRequiredService<IOptionsMonitor<AzureMonitorExporterOptions>>()
            .Get(Options.DefaultName);

        opciones.EnableTraceBasedLogsSampler.Should().BeFalse();
    }

    // Restriccion (MEF-ADR-0038 seccion 9): el overload con callback -- el que exige el flip de
    // arriba -- NO registra DefaultAzureMonitorExporterOptions, que es quien lee
    // APPLICATIONINSIGHTS_CONNECTION_STRING directo del entorno. La connection string pasa a llegar
    // por un unico camino: IConfiguration, poblada por el proveedor de variables de entorno del
    // Host.CreateApplicationBuilder del worker. Un host sin ese proveedor apagaria la exportacion
    // completa EN SILENCIO, y los guardrails de TracerProvider de arriba seguirian verdes.
    [Fact]
    public void ConfigurarObservabilidad_ResuelveLaConnectionStringDelEntorno_EnLasOpcionesDelExporter()
    {
        ConVariableDeEntorno(VariableConnectionString, ConnectionStringAppInsightsDummy, () =>
        {
            using var provider = ComponerServiceProvider();

            var opciones = provider.GetRequiredService<IOptionsMonitor<AzureMonitorExporterOptions>>()
                .Get(Options.DefaultName);

            opciones.ConnectionString.Should().Be(ConnectionStringAppInsightsDummy);
        });
    }

    // Restriccion: el flip de arriba habilita el paso de TODOS los LogRecord del daemon, incluidos
    // sus Information rutinarios (min_replicas = 1, 24/7) -- presion sobre el daily cap
    // (CA-ADR-0009 Capa 3). El filtro sube el piso de EXPORTACION de esas categorias a Warning
    // conservando sus LogError. Se verifica el efecto observable (IsEnabled), no el mecanismo:
    // AddFilter<T> es una eleccion del seam, el piso resultante es el contrato.
    [Fact]
    public void ConfigurarObservabilidad_SubeANivelWarningElPisoDeExportacion_CuandoLaCategoriaEsDelDaemonJasperFx() =>
        VerificarPisoDeExportacionEnWarning("JasperFx.Events.Daemon.HighWater.HighWaterAgent");

    [Fact]
    public void ConfigurarObservabilidad_SubeANivelWarningElPisoDeExportacion_CuandoLaCategoriaEsDelDaemonMarten() =>
        VerificarPisoDeExportacionEnWarning("Marten.Events.Daemon.HighWater.HighWaterAgent");

    // Control del filtro anterior: focalizado a las categorias del daemon, no un downgrade global
    // del piso de exportacion -- eso apagaria la observabilidad del codigo propio del worker, que
    // no exhibe el modo de perdida que este desacople corrige.
    [Fact]
    public void ConfigurarObservabilidad_ConservaElPisoInformation_ParaCategoriasFueraDelDaemon()
    {
        using var provider = ComponerServiceProvider();
        var loggerFactory = provider.GetRequiredService<ILoggerFactory>();

        var logger = loggerFactory.CreateLogger(NombreServicioComoCategoria);

        logger.IsEnabled(LogLevel.Information).Should().BeTrue();
    }

    private const string NombreServicioComoCategoria =
        ConfiguracionObservabilidadProjections.NombreServicio + ".Worker";

    private static void VerificarPisoDeExportacionEnWarning(string categoriaDelDaemon)
    {
        using var provider = ComponerServiceProvider();
        var loggerFactory = provider.GetRequiredService<ILoggerFactory>();

        var logger = loggerFactory.CreateLogger(categoriaDelDaemon);

        logger.IsEnabled(LogLevel.Information).Should().BeFalse();
        logger.IsEnabled(LogLevel.Warning).Should().BeTrue();
    }
}
