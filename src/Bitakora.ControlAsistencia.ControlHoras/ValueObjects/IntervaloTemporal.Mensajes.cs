using System.Resources;

namespace Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

public sealed partial class IntervaloTemporal
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.ControlHoras.ValueObjects.IntervaloTemporalMensajes",
        typeof(IntervaloTemporal).Assembly);

    public static class Mensajes
    {
        public static string InicioDebeSerMenorQueFin =>
            ResourceManager.GetString(nameof(InicioDebeSerMenorQueFin))!;

        // Issue #143: Validacion de Partir(int).
        public static string PuntoDeParticionDebeSerInterior =>
            ResourceManager.GetString(nameof(PuntoDeParticionDebeSerInterior))!;
    }
}
