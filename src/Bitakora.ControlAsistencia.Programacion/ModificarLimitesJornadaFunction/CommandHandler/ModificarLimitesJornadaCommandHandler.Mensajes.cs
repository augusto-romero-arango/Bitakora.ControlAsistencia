using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.ModificarLimitesJornadaFunction.CommandHandler;

public partial class ModificarLimitesJornadaCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.ModificarLimitesJornadaFunction.CommandHandler.ModificarLimitesJornadaCommandHandlerMensajes",
        typeof(ModificarLimitesJornadaCommandHandler).Assembly);
    public static class Mensajes
    {
        public static string JornadaNoEncontrada => ResourceManager.GetString(nameof(JornadaNoEncontrada))!;
    }
}
