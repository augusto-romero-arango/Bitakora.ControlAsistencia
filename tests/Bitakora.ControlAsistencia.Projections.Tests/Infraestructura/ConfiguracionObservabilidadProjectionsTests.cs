// El worker corre sin ingress (MEF-ADR-0034 seccion 8): su observabilidad depende solo de las trazas
// OpenTelemetry. Este test de composicion, hermano del de MEF-ADR-0029, construye el
// IServiceCollection que Program.cs compone y verifica que el TracerProvider se resuelve, sin
// Postgres ni Container App desplegado.
//
// Limite conocido: que Program.cs invoque el seam no se verifica (top-level statements, sin
// equivalente a WebApplicationFactory); lo mas cerca es componer juntos los dos seams del worker.
// El enlace literal queda cubierto por revision de codigo y por el arranque real post-deploy
// (MEF-ADR-0013).
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using AwesomeAssertions;
using Azure.Monitor.OpenTelemetry.Exporter;
using Bitakora.ControlAsistencia.Projections.Infraestructura;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Bitakora.ControlAsistencia.Projections.Tests.Infraestructura;

public class ConfiguracionObservabilidadProjectionsTests
{
    private const string VariableConnectionString = "APPLICATIONINSIGHTS_CONNECTION_STRING";

    // Debe DIFERIR del valor inerte con el que el seam rellena ConnectionString cuando queda vacia:
    // si coincidiera, ResuelveLaConnectionStringDelEntorno pasaria en verde aunque el PostConfigure
    // pisara la connection string real -- exactamente el modo de fallo que ese test vigila.
    private const string ConnectionStringAppInsightsDummy =
        "InstrumentationKey=11111111-2222-3333-4444-555555555555;" +
        "IngestionEndpoint=https://presente.in.applicationinsights.azure.com/";

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

    // UseAzureMonitorExporter llama SetSampler por dentro con RateLimitedSampler (TracesPerSecond
    // tiene valor por defecto 5.0; Azure.Monitor.OpenTelemetry.Exporter 1.8.1) y pisa el sampler que
    // el proyecto fije antes. La correccion es de orden, por eso se verifica el sampler EFECTIVO y no
    // que el codigo llame a SetSampler.
    [Fact]
    public void ConfigurarObservabilidad_ResuelveElSamplerConfiguradoPorElProyecto_EnVezDeRateLimitedSampler()
    {
        using var provider = ComponerServiceProvider();
        var tracerProvider = provider.GetRequiredService<TracerProvider>();

        var samplerEfectivo = SamplerEfectivo.De(tracerProvider);

        // Por nombre completo: RateLimitedSampler es internal en el ensamblado del exporter.
        samplerEfectivo.GetType().FullName.Should().NotBe(
            "Azure.Monitor.OpenTelemetry.Exporter.Internals.RateLimitedSampler");
        samplerEfectivo.Should().BeOfType<SamplerQueDescartaPollingDelDaemon>();
    }

    // Complementa ResolverRatioDeSampling_*, que solo verifican el parsing: este verifica que el ratio
    // llega al sampler efectivo. Description compone la del sampler envuelto:
    // "SamplerQueDescartaPollingDelDaemon{ParentBased{TraceIdRatioBasedSampler{F6}}}".
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

    // TELEMETRY_SAMPLING_RATIO no esta puesta en ningun recurso desplegado, asi que en dev corre el
    // default: un ratio distinto a RatioSamplingPorDefecto solo se veria como volumen de ingestion
    // inesperado.
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

    // El patron de la fuente propia va sin punto antes del asterisco: "X.*" se ancla como ^X\..*$ y
    // descarta la ActivitySource llamada exactamente "X", el nombre del ensamblado (verificado contra
    // OpenTelemetry 1.16.0). No "restaurar la paridad" con ControlHoras: los spans se perderian en
    // silencio. Usa su propio TracerProvider porque el del contenedor lleva el sampler de ratio y
    // volveria la asercion probabilistica.
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

    // Dos niveles: ConfigurarObservabilidad_* lee el Resource que termino usando el TracerProvider del
    // contenedor (GetResource, la via por la que el exporter deriva cloud_RoleName), y
    // ConfigurarRecurso_* aisla la precedencia frente al entorno sin construir el contenedor.
    // CreateDefault() y no CreateEmpty(): es lo que usa el SDK e incluye el fallback `unknown_service:`
    // y el detector de OTEL_SERVICE_NAME/OTEL_RESOURCE_ATTRIBUTES.

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

    [Fact]
    public void NombreServicio_CoincideConElNombreDelEnsambladoDelWorker()
    {
        var nombreDelEnsamblado = typeof(ConfiguracionObservabilidadProjections).Assembly.GetName().Name!;

        ConfiguracionObservabilidadProjections.NombreServicio.Should().Be(nombreDelEnsamblado);
    }

    // No puede fallar mientras PatronFuentePropia se derive de NombreServicio: impide volver a escribir
    // el literal a mano en una de las dos constantes y dejarlas divergir.
    [Fact]
    public void PatronFuentePropia_CoincideConNombreServicioMasAsterisco()
    {
        ConfiguracionObservabilidadProjections.PatronFuentePropia.Should()
            .Be(ConfiguracionObservabilidadProjections.NombreServicio + "*");
    }

    // CreateDefault() ya parsea OTEL_SERVICE_NAME: falla si ConfigurarRecurso corriera antes que el
    // detector de entorno o si AddService no sobrescribiera el atributo.
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

    // Con el overload con callback la connection string del entorno debe seguir llegando a las
    // opciones, y el fallback dummy de PostConfigure no debe pisarla: si falla, la exportacion se
    // apaga en silencio y los guardrails de TracerProvider de arriba siguen verdes.
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

    // Restriccion: el flip de arriba deja pasar tambien los Information rutinarios del daemon
    // (min_replicas = 1, 24/7) contra el daily cap (CA-ADR-0009 Capa 3). El filtro sube a Warning el
    // piso de exportacion de esas categorias conservando sus LogError. Se verifica el efecto
    // observable (IsEnabled), no el mecanismo.
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

    // Se fija el efecto observable, no el mecanismo: un InMemoryExporter como segundo reader (se
    // acumulan, no se pisan), sin red ni Azure Monitor. El Meter de prueba se registra con AddMeter:
    // sin esa linea los tests pasarian en verde aun sin supresion. Los nombres GC van literales: esa
    // familia es el contrato que se conserva.
    private const string NombreMeterDePrueba =
        "Bitakora.ControlAsistencia.Projections.Tests.MeterArbitrarioDePrueba";

    private const string InstrumentoGcCollections = "dotnet.gc.collections";
    private const string InstrumentoGcHeapSize = "dotnet.gc.last_collection.heap.size";
    private const string InstrumentoGcHeapFragmentation =
        "dotnet.gc.last_collection.heap.fragmentation.size";

    [Fact]
    public void ConfigurarObservabilidad_SuprimeLaExportacionDeMetricas_ParaUnInstrumentoArbitrario()
    {
        var metricasExportadas = new List<Metric>();
        using var provider = ComponerServiceProviderConGuardrailDeMetricas(metricasExportadas);
        var meterProvider = provider.GetRequiredService<MeterProvider>();

        using var meter = new Meter(NombreMeterDePrueba);
        var contador = meter.CreateCounter<long>("cualquier.instrumento.de.prueba");
        contador.Add(1);
        meterProvider.ForceFlush();

        metricasExportadas.Should().BeEmpty();
    }

    [Fact]
    public void ConfigurarObservabilidad_ExportaSoloElInstrumentoDeLaFamiliaGC_CuandoElInstrumentoCuentaColecciones() =>
        VerificarQueSoloSobreviveElInstrumentoGc(InstrumentoGcCollections);

    [Fact]
    public void ConfigurarObservabilidad_ExportaSoloElInstrumentoDeLaFamiliaGC_CuandoElInstrumentoMideElHeap() =>
        VerificarQueSoloSobreviveElInstrumentoGc(InstrumentoGcHeapSize);

    [Fact]
    public void ConfigurarObservabilidad_ExportaSoloElInstrumentoDeLaFamiliaGC_CuandoElInstrumentoMideLaFragmentacionDelHeap() =>
        VerificarQueSoloSobreviveElInstrumentoGc(InstrumentoGcHeapFragmentation);

    // El instrumento arbitrario se emite JUNTO al de la familia GC en la misma composicion: es lo
    // que demuestra que la supresion es SELECTIVA. Emitir solo el GC pasaria en verde tambien con
    // una implementacion que no suprimiera nada.
    private static void VerificarQueSoloSobreviveElInstrumentoGc(string instrumentoGc)
    {
        var metricasExportadas = new List<Metric>();
        using var provider = ComponerServiceProviderConGuardrailDeMetricas(metricasExportadas);
        var meterProvider = provider.GetRequiredService<MeterProvider>();

        using var meter = new Meter(NombreMeterDePrueba);
        meter.CreateCounter<long>("cualquier.instrumento.de.prueba").Add(1);
        meter.CreateCounter<long>(instrumentoGc).Add(1);
        meterProvider.ForceFlush();

        metricasExportadas.Select(m => m.Name).Should().BeEquivalentTo([instrumentoGc]);
    }

    // La supresion de metricas no debe apagar el tracing (CA-ADR-0009 Capa 2) como efecto colateral.
    [Fact]
    public void ConfigurarObservabilidad_ConservaElTracerProviderDelContenedor_CuandoSeAgregaElGuardrailDeMetricas()
    {
        var metricasExportadas = new List<Metric>();
        using var provider = ComponerServiceProviderConGuardrailDeMetricas(metricasExportadas);

        provider.GetService<TracerProvider>().Should().NotBeNull();
    }

    private static ServiceProvider ComponerServiceProviderConGuardrailDeMetricas(
        ICollection<Metric> metricasExportadas) =>
        ComponerServiceProvider(services => services
            .ConfigureOpenTelemetryMeterProvider(builder => builder
                .AddMeter(NombreMeterDePrueba)
                .AddInMemoryExporter(metricasExportadas)));
}
