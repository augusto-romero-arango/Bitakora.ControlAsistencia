using System.Resources;

namespace Bitakora.ControlAsistencia.Sedes;

public partial class ReadyCheck
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Sedes.ReadyCheckMensajes",
        typeof(ReadyCheck).Assembly);

    public static class Mensajes
    {
        public static string EventStoreNoDisponible =>
            ResourceManager.GetString(nameof(EventStoreNoDisponible))!;
    }
}
