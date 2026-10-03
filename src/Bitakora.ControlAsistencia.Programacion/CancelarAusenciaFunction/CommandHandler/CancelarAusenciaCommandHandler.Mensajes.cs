using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.CancelarAusenciaFunction.CommandHandler;

public partial class CancelarAusenciaCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.CancelarAusenciaFunction.CommandHandler.CancelarAusenciaCommandHandlerMensajes",
        typeof(CancelarAusenciaCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string AusenciaNoEncontrada =>
            ResourceManager.GetString(nameof(AusenciaNoEncontrada))!;
    }
}
