using System.Resources;

namespace Bitakora.ControlAsistencia.Sedes.RegistrarSedeFunction.CommandHandler;

public partial class RegistrarSedeCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Sedes.RegistrarSedeFunction.CommandHandler.RegistrarSedeCommandHandlerMensajes",
        typeof(RegistrarSedeCommandHandler).Assembly);

    // internal, no private: los tests lo afirman via InternalsVisibleTo.
    internal static class Mensajes
    {
        public static string SedeYaRegistrada =>
            ResourceManager.GetString(nameof(SedeYaRegistrada))!;
    }
}
