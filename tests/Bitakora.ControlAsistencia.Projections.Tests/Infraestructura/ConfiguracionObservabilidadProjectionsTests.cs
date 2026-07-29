// Issue #250: instrumentar el worker de proyecciones con Application Insights.
// Issue #263: fijar el recurso `service.name` para que el worker deje de aparecer en Application
// Insights como `unknown_service:dotnet`. Guardrails al final de esta misma clase (CA-2 a CA-4) --
// misma familia de tests, no una clase nueva.
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
// 1. Que el ratio resuelto llegue efectivamente al ParentBasedSampler/TraceIdRatioBasedSampler no
//    se verifica: el sampler compuesto vive dentro de TracerProviderSdk, que OpenTelemetry no
//    expone publicamente. Ejercitarlo end-to-end exigiria muestrear actividades reales contra un
//    ratio fraccionario -- probabilistico y flaky. Queda cubierto por revision de codigo.
// 2. Que Program.cs invoque el seam (CA-4) tampoco: Program.cs son top-level statements y el
//    worker no tiene equivalente a WebApplicationFactory (limite que MEF-ADR-0029 ya reconoce).
//    Lo mas cerca que llega esta clase es componer juntos los dos seams del worker
//    (ConfigurarEventos + ConfigurarObservabilidad), que es lo que Program.cs encadena; el enlace
//    literal queda cubierto por revision de codigo (CA-4) y por el arranque real post-deploy
//    (MEF-ADR-0013).
using System.Diagnostics;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Projections.Infraestructura;
using Microsoft.Extensions.DependencyInjection;
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

    // --- Recurso de OpenTelemetry: service.name (issue #263, CA-2 a CA-4) ---
    //
    // Estos tests invocan ConfigurarRecurso directamente sobre un ResourceBuilder propio -- no a
    // traves de ConfigurarObservabilidad ni del ServiceProvider completo. Wirear el AddService
    // inline en el lambda de tracing (como ya pasa con el sampler compuesto, ver limite 1 de la
    // cabecera de este archivo) dejaria el recurso sin cobertura: TracerProvider no expone
    // publicamente el Resource que termino usando. CreateDefault() (no CreateEmpty()) es
    // deliberado: es lo que ConfigurarObservabilidad usaria en produccion via ConfigureResource, e
    // incluye el fallback `unknown_service:` y el detector de OTEL_SERVICE_NAME/
    // OTEL_RESOURCE_ATTRIBUTES (verificado contra el XML doc de OpenTelemetry 1.16.0) -- exactamente
    // lo que CA-2 y CA-4 necesitan para demostrar que el nombre del codigo gana sobre ambos.

    private const string AtributoServiceName = "service.name";
    private const string AtributoServiceNamespace = "service.namespace";
    private const string AtributoServiceInstanceId = "service.instance.id";
    private const string VariableOtelServiceName = "OTEL_SERVICE_NAME";

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
    // ActivitySource. Si diverge, el proximo test (PatronFuentePropia_...) tambien lo detecta.
    [Fact]
    public void NombreServicio_CoincideConElNombreDelEnsambladoDelWorker()
    {
        var nombreDelEnsamblado = typeof(ConfiguracionObservabilidadProjections).Assembly.GetName().Name!;

        ConfiguracionObservabilidadProjections.NombreServicio.Should().Be(nombreDelEnsamblado);
    }

    // CA-3: PatronFuentePropia (issue #250) hoy repite el nombre del ensamblado a mano. Este
    // guardrail impide que NombreServicio y PatronFuentePropia diverjan con el tiempo -- si alguien
    // cambia uno sin el otro, este test falla. La forma de hacerlo pasar de manera sostenible es
    // derivar PatronFuentePropia de NombreServicio (p.ej. `NombreServicio + "*"`), no editar ambos
    // strings a mano en cada cambio.
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
}
