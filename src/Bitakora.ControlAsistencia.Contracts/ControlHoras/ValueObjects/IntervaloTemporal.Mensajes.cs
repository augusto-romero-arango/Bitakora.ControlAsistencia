using System.Resources;

namespace Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

public sealed partial class IntervaloTemporal
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects.IntervaloTemporalMensajes",
        typeof(IntervaloTemporal).Assembly);

    public static class Mensajes
    {
        public static string InicioDebeSerMenorQueFin =>
            ResourceManager.GetString(nameof(InicioDebeSerMenorQueFin))!;
    }
}
