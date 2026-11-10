using System.Resources;

namespace Bitakora.ControlAsistencia.Colaboradores.CorregirNombresFunction.CommandHandler;

public partial class CorregirNombresCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Colaboradores.CorregirNombresFunction.CommandHandler.CorregirNombresCommandHandlerMensajes",
        typeof(CorregirNombresCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string ColaboradorNoEncontrado =>
            ResourceManager.GetString(nameof(ColaboradorNoEncontrado))!;
    }
}
