using System.Resources;

namespace Bitakora.ControlAsistencia.ControlHoras;

public partial class ReadyCheck
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.ControlHoras.ReadyCheckMensajes",
        typeof(ReadyCheck).Assembly);

    public static class Mensajes
    {
        public static string EventStoreNoDisponible =>
            ResourceManager.GetString(nameof(EventStoreNoDisponible))!;
    }
}
