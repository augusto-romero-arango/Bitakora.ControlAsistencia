using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed partial class Turno
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.DomainEvents.TurnoMensajes",
        typeof(Turno).Assembly);

    internal static class Mensajes
    {
        public static string LabelDescanso =>
            ResourceManager.GetString(nameof(LabelDescanso))!;

        public static string LabelIncompleto =>
            ResourceManager.GetString(nameof(LabelIncompleto))!;
    }
}
