// Issue #250: instrumentar el worker de proyecciones con Application Insights.
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
// sin mutar variables de entorno del proceso, evitando interferencia con otras clases de test que
// puedan correr en paralelo.
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Projections.Infraestructura;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace Bitakora.ControlAsistencia.Projections.Tests.Infraestructura;

public class ConfiguracionObservabilidadProjectionsTests
{
    private const string ConnectionStringDummy =
        "InstrumentationKey=00000000-0000-0000-0000-000000000000;" +
        "IngestionEndpoint=https://dummy.in.applicationinsights.azure.com/";

    private static ServiceProvider ComponerServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.ConfigurarObservabilidad();

        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    // --- Composicion del seam (CA-2, CA-4, CA-5) ---

    [Fact]
    public void ConfigurarObservabilidad_ComponeElContenedorSinExcepciones_ConValidateOnBuild()
    {
        var act = () => ComponerServiceProvider().Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public void ConfigurarObservabilidad_ResuelveTracerProviderDelContenedor()
    {
        using var provider = ComponerServiceProvider();

        var tracerProvider = provider.GetService<TracerProvider>();

        tracerProvider.Should().NotBeNull();
    }

    [Fact]
    public void ConfigurarObservabilidad_ResuelveTracerProviderDelContenedor_CuandoLaConnectionStringEstaPresente()
    {
        var original = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
        try
        {
            // Replica el escenario real (MEF-ADR-0025/CA-ADR-0026): el Container App inyecta esta
            // variable via Key Vault reference; este test no la lee a mano, solo verifica que el
            // exporter no revienta la composicion cuando el runtime ya la puso en el entorno.
            Environment.SetEnvironmentVariable(
                "APPLICATIONINSIGHTS_CONNECTION_STRING", ConnectionStringDummy);

            using var provider = ComponerServiceProvider();

            var tracerProvider = provider.GetService<TracerProvider>();

            tracerProvider.Should().NotBeNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING", original);
        }
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

    // --- Seam de nivel BC (CA-4): Program.cs debe invocar ConfigurarObservabilidad junto a
    // ConfigurarEventos. Esta guarda documenta el requisito -- MEF-ADR-0029 no puede testear
    // Program.cs directamente (top-level statements, sin equivalente a WebApplicationFactory para
    // el worker), asi que el enlace real entre Program.cs y este seam queda cubierto por el smoke
    // test post-deploy (MEF-ADR-0013) y por revision de codigo (CA-4), no por este test unitario.
}
