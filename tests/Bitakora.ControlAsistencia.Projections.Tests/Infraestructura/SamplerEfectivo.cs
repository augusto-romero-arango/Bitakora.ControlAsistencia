// Issue #308: helper de test (no es una clase de tests) para leer el sampler que quedo realmente
// instalado en un TracerProvider construido. Lo consume ConfiguracionObservabilidadProjectionsTests
// (CA-1, CA-3) y vive aparte -- no dentro de una clase de tests -- porque el sujeto que describe es
// el TracerProvider, no el sampler del daemon: cualquier guardrail futuro sobre el wiring OTel del
// worker lo necesita igual, y ninguna clase de tests deberia ser el hogar de utilidades que otra
// clase de tests consume.
using System.Reflection;
using OpenTelemetry.Trace;

namespace Bitakora.ControlAsistencia.Projections.Tests.Infraestructura;

internal static class SamplerEfectivo
{
    /// <summary>
    /// Lee la propiedad interna `Sampler` de `TracerProviderSdk` (OpenTelemetry.dll no la expone
    /// publicamente). Determinista: permite comparar tipos y leer `Sampler.Description` (publica),
    /// sin muestrear actividades reales contra un ratio fraccionario. Este acceso no publico es lo
    /// que vuelve verificable el hallazgo 1 del issue #308 -- que `UseAzureMonitorExporter()` pisaba
    /// el sampler del proyecto -- que la revision de codigo no podia atrapar.
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
