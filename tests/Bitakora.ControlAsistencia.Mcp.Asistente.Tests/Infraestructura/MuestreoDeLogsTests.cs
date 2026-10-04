using AwesomeAssertions;
using Azure.Monitor.OpenTelemetry.Exporter;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Infraestructura;

public class MuestreoDeLogsTests
{
    [Fact]
    public void ConfigurarObservabilidadMcp_DesactivaElSamplerDeLogsPorTraza_ParaNoPerderLogsDeTrazasNoMuestreadas()
    {
        var services = new ServiceCollection();
        services.ConfigurarObservabilidadMcp();
        using var provider = services.BuildServiceProvider();

        var opciones = provider.GetRequiredService<IOptions<AzureMonitorExporterOptions>>().Value;

        opciones.EnableTraceBasedLogsSampler.Should().BeFalse();
    }
}
