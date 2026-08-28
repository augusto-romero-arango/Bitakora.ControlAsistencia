using System.Resources;

namespace Bitakora.ControlAsistencia.Sedes.DesactivarSedeFunction.CommandHandler;

public partial class DesactivarSedeCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Sedes.DesactivarSedeFunction.CommandHandler.DesactivarSedeCommandHandlerMensajes",
        typeof(DesactivarSedeCommandHandler).Assembly);

    // internal, no private: los tests afirman los mensajes del 404/409 via InternalsVisibleTo.
    internal static class Mensajes
    {
        public static string SedeNoEncontrada =>
            ResourceManager.GetString(nameof(SedeNoEncontrada))!;

        public static string SedeYaInactiva =>
            ResourceManager.GetString(nameof(SedeYaInactiva))!;
    }
}
