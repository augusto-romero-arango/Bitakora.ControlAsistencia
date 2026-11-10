using System.Resources;

namespace Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

public sealed partial record MomentoDelDia
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.ControlHoras.ValueObjects.MomentoDelDiaMensajes",
        typeof(MomentoDelDia).Assembly);

    public static class Mensajes
    {
        public static string MinutosAbsolutosDebeSerPositivoOCero =>
            ResourceManager.GetString(nameof(MinutosAbsolutosDebeSerPositivoOCero))!;
    }
}
