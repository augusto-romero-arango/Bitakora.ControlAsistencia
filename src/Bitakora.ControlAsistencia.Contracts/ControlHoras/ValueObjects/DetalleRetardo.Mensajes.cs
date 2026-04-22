using System.Resources;

namespace Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

public sealed partial record DetalleRetardo
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects.DetalleRetardoMensajes",
        typeof(DetalleRetardo).Assembly);

    public static class Mensajes
    {
        public static string CompensadosExcedenRetardados =>
            ResourceManager.GetString(nameof(CompensadosExcedenRetardados))!;
    }
}
