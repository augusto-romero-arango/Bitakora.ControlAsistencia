using System.Resources;

namespace Bitakora.ControlAsistencia.Colaboradores.RegistrarColaboradorFunction.CommandHandler;

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
