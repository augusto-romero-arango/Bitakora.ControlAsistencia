using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed partial class ModalidadJornada
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.DomainEvents.ModalidadJornadaMensajes",
        typeof(ModalidadJornada).Assembly);

    internal static class Mensajes
    {
        public static string LabelModalidad =>
            ResourceManager.GetString(nameof(LabelModalidad))!;

        public static string NombreNoReconocido =>
            ResourceManager.GetString(nameof(NombreNoReconocido))!;
    }
}
