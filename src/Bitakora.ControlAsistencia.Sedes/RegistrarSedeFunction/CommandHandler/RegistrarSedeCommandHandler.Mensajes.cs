using System.Resources;

namespace Bitakora.ControlAsistencia.Sedes.RegistrarSedeFunction.CommandHandler;

// MEF-ADR-0009: mensajes del handler en .resx separado.
// internal: accesible desde tests via InternalsVisibleTo en el .csproj.
public partial class RegistrarSedeCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Sedes.RegistrarSedeFunction.CommandHandler.RegistrarSedeCommandHandlerMensajes",
        typeof(RegistrarSedeCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string SedeYaRegistrada =>
            ResourceManager.GetString(nameof(SedeYaRegistrada))!;
    }
}
