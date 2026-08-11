using System.Resources;

namespace Bitakora.ControlAsistencia.Colaboradores.RegistrarColaboradorFunction.CommandHandler;

// MEF-ADR-0009: mensajes del handler en .resx separado.
// internal: accesible desde tests via InternalsVisibleTo en el .csproj.
public partial class RegistrarColaboradorCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Colaboradores.RegistrarColaboradorFunction.CommandHandler.RegistrarColaboradorCommandHandlerMensajes",
        typeof(RegistrarColaboradorCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string ColaboradorYaRegistrado =>
            ResourceManager.GetString(nameof(ColaboradorYaRegistrado))!;
    }
}
