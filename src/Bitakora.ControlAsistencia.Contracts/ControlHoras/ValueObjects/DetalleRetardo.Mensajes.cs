using System.Resources;

namespace Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

public sealed partial class DetalleRetardo
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects.DetalleRetardoMensajes",
        typeof(DetalleRetardo).Assembly);

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
