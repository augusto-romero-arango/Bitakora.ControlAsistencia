// UseAzureMonitorExporter() activa el pipeline de metricas aunque solo se invoque .WithTracing(...)
// (verificado descompilando Azure.Monitor.OpenTelemetry.Exporter 1.8.1), y eso producia ~76.000
// metricas/dia por app (CA-ADR-0009). Decision de producto: recortar TODO, no una familia puntual.
//
// El guardrail verifica el efecto, no el mecanismo: ninguna metrica, ni una arbitraria, llega a un
// InMemoryExporter agregado como segundo reader (los ConfigureOpenTelemetryMeterProvider se acumulan,
// no se pisan), sin red ni Azure Monitor.
//
// El Meter de prueba se registra EXPLICITAMENTE con AddMeter: el MeterProviderBuilder solo escucha los
// meters suscritos y sin esa linea el test pasaria en verde aun sin supresion. Es un meter propio para
// no acoplar el guardrail a nombres del runtime que .NET puede renombrar entre versiones.

using System.Diagnostics.Metrics;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class SupresionMetricasOTelTests
{
    private const string MartenConnectionStringDummy =
        "Host=dummy;Port=5432;Database=dummy;Username=dummy;Password=dummy";

    private const string ServiceBusConnectionStringDummy =
        "Endpoint=sb://dummy.servicebus.windows.net/;SharedAccessKeyName=dummy;SharedAccessKey=dummy";

    private const string NombreMeterDePrueba =
        "Bitakora.ControlAsistencia.ControlHoras.Tests.MeterArbitrarioDePrueba";

    private static ServiceProvider ComponerServiceProvider(ICollection<Metric> metricasExportadas)
    {
        var services = new ServiceCollection();

        services.AgregarServiciosControlHoras(
            MartenConnectionStringDummy,
            ServiceBusConnectionStringDummy,
            isDev: true);

        services.ConfigureOpenTelemetryMeterProvider(builder => builder
            .AddMeter(NombreMeterDePrueba)
            .AddInMemoryExporter(metricasExportadas));

        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Fact]
    public void AgregarServiciosControlHoras_SuprimeLaExportacionDeMetricas_ParaCualquierInstrumento()
    {
        var metricasExportadas = new List<Metric>();
        using var provider = ComponerServiceProvider(metricasExportadas);
        var meterProvider = provider.GetRequiredService<MeterProvider>();

        using var meter = new Meter(NombreMeterDePrueba);
        var contador = meter.CreateCounter<long>("cualquier.instrumento.de.prueba");
        contador.Add(1);
        meterProvider.ForceFlush();

        metricasExportadas.Should().BeEmpty();
    }

    // La supresion de metricas no debe tocar el tracing (Capa 2, CA-ADR-0009).
    [Fact]
    public void AgregarServiciosControlHoras_ConservaElTracerProviderDelContenedor()
    {
        var metricasExportadas = new List<Metric>();
        using var provider = ComponerServiceProvider(metricasExportadas);

        var tracerProvider = provider.GetService<TracerProvider>();

        tracerProvider.Should().NotBeNull();
    }
}
