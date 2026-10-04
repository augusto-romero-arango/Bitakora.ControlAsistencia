using System.Globalization;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

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
                .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingRatio))))
            .WithMetrics(metrics => metrics.AddView(instrumentName: "*", MetricStreamConfiguration.Drop));

        // El reader de metricas del exporter se construye de forma sincronica y exige una connection
        // string o lanza: sin este fallback, un arranque en frio con la Key Vault reference sin
        // resolver tumbaria el host (CA-ADR-0009, actualizacion 2026-06-18). Nunca pisa una real.
        services.PostConfigure<AzureMonitorExporterOptions>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
                options.ConnectionString =
                    "InstrumentationKey=00000000-0000-0000-0000-000000000000;" +
                    "IngestionEndpoint=https://dummy.in.applicationinsights.azure.com/";
        });

        return services;
    }
}
