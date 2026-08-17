using System.Resources;

namespace Bitakora.ControlAsistencia.Colaboradores;

public partial class ReadyCheck
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Colaboradores.ReadyCheckMensajes",
        typeof(ReadyCheck).Assembly);

    internal static class Mensajes
    {
        public static string EventStoreNoDisponible =>
            ResourceManager.GetString(nameof(EventStoreNoDisponible))!;
    }
}
