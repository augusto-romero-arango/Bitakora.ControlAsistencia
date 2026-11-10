using System.Reflection;
using OpenTelemetry.Trace;

namespace Bitakora.ControlAsistencia.Projections.Tests.Infraestructura;

internal static class SamplerEfectivo
{
    /// <summary>
    /// Lee la propiedad interna `Sampler` de `TracerProviderSdk` (no publica en OpenTelemetry.dll) para
    /// comparar el sampler instalado de forma determinista, sin muestrear actividades. Es lo que vuelve
    /// verificable que `UseAzureMonitorExporter()` pisaba el sampler del proyecto.
    /// </summary>
    internal static Sampler De(TracerProvider tracerProvider)
    {
        var propiedad = tracerProvider.GetType()
            .GetProperty("Sampler", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                "TracerProviderSdk ya no expone la propiedad interna 'Sampler' " +
                "(OpenTelemetry 1.16.0) -- actualizar este helper de reflection.");

        return (Sampler)propiedad.GetValue(tracerProvider)!;
    }
}
