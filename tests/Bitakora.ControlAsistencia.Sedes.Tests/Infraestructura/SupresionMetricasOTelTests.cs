// Issue #515: UseAzureMonitorExporter() activa el pipeline de metricas automaticamente (verificado
// descompilando Azure.Monitor.OpenTelemetry.Exporter 1.8.1,
// OpenTelemetryBuilderExtensions.UseAzureMonitorExporter: llama WithMetrics(WithTracing(...))
// internamente aunque el proyecto solo invoque .WithTracing(...)), y eso es lo que produce las
// ~76.000 metricas/dia medidas por app (CA-ADR-0009, actualizacion del episodio de
// ingestion-warning). Decision de producto: recortar TODO, no una familia puntual.
//
// El guardrail no asume el mecanismo exacto de supresion (AddView con wildcard, o cualquier otro):
// verifica el efecto observable -- que NINGUNA metrica, ni siquiera una arbitraria que nadie
// anticipo, llega al exporter -- componiendo el contenedor real y sumando un
// ConfigureOpenTelemetryMeterProvider adicional (se acumulan, no se pisan) que registra un
// InMemoryExporter como segundo reader, sin tocar el reader real de Azure Monitor ni requerir red.
//
// El Meter de prueba se agrega EXPLICITAMENTE via AddMeter para simular un instrumento cualquiera
// que SI esta siendo escuchado por el MeterProviderBuilder -- igual que los meters reales del
// runtime/ASP.NET Core que el exporter engancha por su cuenta y que este test no puede nombrar sin
// acoplarse a nombres que .NET/ASP.NET Core podrian renombrar entre versiones (dotnet.gc.*,
// kestrel.*, etc. -- ver CA-ADR-0009). Si el instrumento arbitrario de prueba sigue llegando al
// exporter, la supresion no cubre "TODO" como exige la decision de producto.

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

    // CA-3: la supresion de metricas no debe tocar el tracing (Capa 2, CA-ADR-0009) como efecto
    // colateral -- el TracerProvider sigue siendo parte del mismo seam de observabilidad.
    [Fact]
    public void AgregarServiciosSedes_ConservaElTracerProviderDelContenedor()
    {
        var metricasExportadas = new List<Metric>();
        using var provider = ComponerServiceProvider(metricasExportadas);

        var tracerProvider = provider.GetService<TracerProvider>();

        tracerProvider.Should().NotBeNull();
    }
}
