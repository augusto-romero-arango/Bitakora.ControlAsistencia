// UseAzureMonitorExporter() activa el pipeline de metricas aunque solo se invoque .WithTracing(...)
// (verificado descompilando Azure.Monitor.OpenTelemetry.Exporter 1.8.1); la decision es recortar todo.
// Se verifica el efecto, no el mecanismo: un segundo reader InMemoryExporter (los
// ConfigureOpenTelemetryMeterProvider se acumulan) no debe recibir ninguna metrica. El Meter de prueba
// va con AddMeter porque el provider solo escucha meters suscritos: sin eso el test pasaria aun sin
// supresion. Es propio para no acoplarse a nombres del runtime que .NET puede renombrar.

using System.Diagnostics.Metrics;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.Infraestructura;

public class SupresionMetricasOTelTests
{
    private const string MartenConnectionStringDummy =
        "Host=dummy;Port=5432;Database=dummy;Username=dummy;Password=dummy";

    private const string ServiceBusConnectionStringDummy =
        "Endpoint=sb://dummy.servicebus.windows.net/;SharedAccessKeyName=dummy;SharedAccessKey=dummy";

    private const string NombreMeterDePrueba =
        "Bitakora.ControlAsistencia.Colaboradores.Tests.MeterArbitrarioDePrueba";

    private static ServiceProvider ComponerServiceProvider(ICollection<Metric> metricasExportadas)
    {
        var services = new ServiceCollection();

        services.AgregarServiciosColaboradores(
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
    public void AgregarServiciosColaboradores_SuprimeLaExportacionDeMetricas_ParaCualquierInstrumento()
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

    [Fact]
    public void AgregarServiciosColaboradores_ConservaElTracerProviderDelContenedor()
    {
        var metricasExportadas = new List<Metric>();
        using var provider = ComponerServiceProvider(metricasExportadas);

        var tracerProvider = provider.GetService<TracerProvider>();

        tracerProvider.Should().NotBeNull();
    }
}
