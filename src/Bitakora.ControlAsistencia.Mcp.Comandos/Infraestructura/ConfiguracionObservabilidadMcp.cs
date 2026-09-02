using System.Globalization;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

/// <summary>
/// Seam de observabilidad del servidor (MEF-ADR-0029). El ratio de sampling es politica de
/// costos del CONSUMIDOR (MEF-ADR-0038): default 0.2 cuando TELEMETRY_SAMPLING_RATIO no esta
/// declarada o es invalida.
/// </summary>
public static class ConfiguracionObservabilidadMcp
{
    public static IServiceCollection ConfigurarObservabilidadMcp(this IServiceCollection services)
    {
        var samplingRatio = double.TryParse(
            Environment.GetEnvironmentVariable("TELEMETRY_SAMPLING_RATIO"),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var ratio) && ratio is >= 0.0 and <= 1.0
                ? ratio
                : 0.2;

        services.AddOpenTelemetry()
            .UseFunctionsWorkerDefaults()
            .UseAzureMonitorExporter()
            .WithTracing(tracing => tracing
                .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingRatio))));

        return services;
    }
}
