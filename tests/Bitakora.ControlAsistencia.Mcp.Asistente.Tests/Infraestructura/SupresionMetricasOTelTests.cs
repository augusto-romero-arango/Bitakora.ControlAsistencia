using System.Diagnostics.Metrics;
using System.Reflection;
using AwesomeAssertions;
using Azure.Monitor.OpenTelemetry.Exporter;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Infraestructura;

public class SupresionMetricasOTelTests
{
    private const string NombreMeterDePrueba =
        "Bitakora.ControlAsistencia.Mcp.Asistente.Tests.MeterArbitrarioDePrueba";

    private const string VariableConnectionStringAppInsights = "APPLICATIONINSIGHTS_CONNECTION_STRING";

    private static ServiceProvider ComponerServiceProvider(ICollection<Metric>? metricasExportadas = null)
    {
        var services = new ServiceCollection();

        services.ConfigurarObservabilidadMcp();

        if (metricasExportadas is not null)
        {
            services.ConfigureOpenTelemetryMeterProvider(builder => builder
                .AddMeter(NombreMeterDePrueba)
                .AddInMemoryExporter(metricasExportadas));
        }

        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

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

    private static Sampler ObtenerSamplerEfectivo(TracerProvider tracerProvider)
    {
        var propiedad = tracerProvider.GetType()
            .GetProperty("Sampler", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                "TracerProviderSdk ya no expone la propiedad interna 'Sampler'.");

        return (Sampler)propiedad.GetValue(tracerProvider)!;
    }

    [Fact]
    public void ConfigurarObservabilidadMcp_SuprimeLaExportacionDeMetricas_ParaCualquierInstrumento()
    {
        var metricasExportadas = new List<Metric>();
        ConVariableDeEntorno(VariableConnectionStringAppInsights, null, () =>
        {
            using var provider = ComponerServiceProvider(metricasExportadas);
            var meterProvider = provider.GetRequiredService<MeterProvider>();

            using var meter = new Meter(NombreMeterDePrueba);
            var contador = meter.CreateCounter<long>("cualquier.instrumento.de.prueba");
            contador.Add(1);
            meterProvider.ForceFlush();

            metricasExportadas.Should().BeEmpty();
        });
    }

    [Fact]
    public void ConfigurarObservabilidadMcp_ConservaElTracerProviderYSuSampler_CuandoSeSuprimenLasMetricas()
    {
        ConVariableDeEntorno(VariableConnectionStringAppInsights, null, () =>
        {
            using var provider = ComponerServiceProvider(new List<Metric>());

            var tracerProvider = provider.GetService<TracerProvider>();

            tracerProvider.Should().NotBeNull();
            ObtenerSamplerEfectivo(tracerProvider!).Description.Should().StartWith("ParentBased");
        });
    }

    [Fact]
    public void ConfigurarObservabilidadMcp_ConservaLaConnectionStringReal_CuandoLaVariableDeEntornoEstaPresente()
    {
        const string connectionStringReal =
            "InstrumentationKey=11111111-2222-3333-4444-555555555555;" +
            "IngestionEndpoint=https://real.in.applicationinsights.azure.com/";

        ConVariableDeEntorno(VariableConnectionStringAppInsights, connectionStringReal, () =>
        {
            using var provider = ComponerServiceProvider();

            var opciones = provider.GetRequiredService<IOptions<AzureMonitorExporterOptions>>().Value;

            opciones.ConnectionString.Should().Be(connectionStringReal);
        });
    }

    [Fact]
    public void ConfigurarObservabilidadMcp_ComponeElContenedor_CuandoNoHayConnectionString()
    {
        ConVariableDeEntorno(VariableConnectionStringAppInsights, null, () =>
        {
            var act = () => ComponerServiceProvider().GetRequiredService<MeterProvider>();

            act.Should().NotThrow();
        });
    }
}
