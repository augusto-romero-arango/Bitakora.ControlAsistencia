using System.Resources;

namespace Bitakora.ControlAsistencia.Sedes.ActivarSedeFunction.CommandHandler;

public partial class ActivarSedeCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Sedes.ActivarSedeFunction.CommandHandler.ActivarSedeCommandHandlerMensajes",
        typeof(ActivarSedeCommandHandler).Assembly);

    // internal, no private: los tests afirman los mensajes del 404/409 via InternalsVisibleTo.
    internal static class Mensajes
    {
        public static string SedeNoEncontrada =>
            ResourceManager.GetString(nameof(SedeNoEncontrada))!;

        public static string SedeYaActiva =>
            ResourceManager.GetString(nameof(SedeYaActiva))!;
    }
}
