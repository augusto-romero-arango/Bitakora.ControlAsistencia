using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed partial class MotivoAusencia
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.DomainEvents.MotivoAusenciaMensajes",
        typeof(MotivoAusencia).Assembly);

    internal static class Mensajes
    {
        public static string NombreNoReconocido =>
            ResourceManager.GetString(nameof(NombreNoReconocido))!;
    }
}
