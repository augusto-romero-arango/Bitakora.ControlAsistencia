using System.Resources;

namespace Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

// Issue #143: Mensajes de error para MomentoDelDia (ADR-0012).
public sealed partial record MomentoDelDia
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects.MomentoDelDiaMensajes",
        typeof(MomentoDelDia).Assembly);

    public static class Mensajes
    {
        public static string MinutosAbsolutosDebeSerPositivoOCero =>
            ResourceManager.GetString(nameof(MinutosAbsolutosDebeSerPositivoOCero))!;
    }
}
