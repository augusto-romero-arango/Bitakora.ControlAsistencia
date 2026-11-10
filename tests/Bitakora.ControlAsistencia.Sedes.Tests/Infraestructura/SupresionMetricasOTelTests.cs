// UseAzureMonitorExporter() activa el pipeline de metricas aunque el proyecto solo invoque
// .WithTracing(...) (decompilado de Azure.Monitor.OpenTelemetry.Exporter 1.8.1). Decision de
// producto: recortar TODAS las metricas (CA-ADR-0009).
//
// El guardrail verifica el efecto observable, no el mecanismo: un InMemoryExporter como segundo
// reader (se acumulan, no se pisan), sin red ni Azure Monitor. El Meter de prueba se registra con
// AddMeter: sin esa linea el test pasaria en verde aun sin supresion. Se usa un meter propio para
// no acoplarse a nombres del runtime que .NET puede renombrar entre versiones.

using System.Diagnostics.Metrics;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Bitakora.ControlAsistencia.Sedes.Tests.Infraestructura;

public class SupresionMetricasOTelTests
{
    private const string MartenConnectionStringDummy =
        "Host=dummy;Port=5432;Database=dummy;Username=dummy;Password=dummy";

    private const string ServiceBusConnectionStringDummy =
        "Endpoint=sb://dummy.servicebus.windows.net/;SharedAccessKeyName=dummy;SharedAccessKey=dummy";

    private const string NombreMeterDePrueba =
        "Bitakora.ControlAsistencia.Sedes.Tests.MeterArbitrarioDePrueba";

    private static ServiceProvider ComponerServiceProvider(ICollection<Metric> metricasExportadas)
    {
        var services = new ServiceCollection();

        services.AgregarServiciosSedes(
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
    public void AgregarServiciosSedes_SuprimeLaExportacionDeMetricas_ParaCualquierInstrumento()
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

    // La supresion de metricas no debe apagar el tracing (CA-ADR-0009 Capa 2) como efecto colateral.
    [Fact]
    public void AgregarServiciosSedes_ConservaElTracerProviderDelContenedor()
    {
        var metricasExportadas = new List<Metric>();
        using var provider = ComponerServiceProvider(metricasExportadas);

        var tracerProvider = provider.GetService<TracerProvider>();

        tracerProvider.Should().NotBeNull();
    }
}
