using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion;

public partial class ReadyCheck
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.ReadyCheckMensajes",
        typeof(ReadyCheck).Assembly);

    internal static class Mensajes
    {
        public static string EventStoreNoDisponible =>
            ResourceManager.GetString(nameof(EventStoreNoDisponible))!;
    }
}
