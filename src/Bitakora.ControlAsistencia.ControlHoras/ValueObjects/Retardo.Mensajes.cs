using System.Resources;

namespace Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

public sealed partial class Retardo
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.ControlHoras.ValueObjects.RetardoMensajes",
        typeof(Retardo).Assembly);

    public static class Mensajes
    {
        public static string SinRetardo =>
            ResourceManager.GetString(nameof(SinRetardo))!;

        public static string LabelRetardo =>
            ResourceManager.GetString(nameof(LabelRetardo))!;

        public static string LabelCompensado =>
            ResourceManager.GetString(nameof(LabelCompensado))!;

        public static string LabelNeto =>
            ResourceManager.GetString(nameof(LabelNeto))!;
    }
}
