using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.ActivarJornadaFunction.CommandHandler;

public partial class ActivarJornadaCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.ActivarJornadaFunction.CommandHandler.ActivarJornadaCommandHandlerMensajes",
        typeof(ActivarJornadaCommandHandler).Assembly);

    public static class Mensajes
    {
        public static string JornadaNoEncontrada => ResourceManager.GetString(nameof(JornadaNoEncontrada))!;
    }
}
