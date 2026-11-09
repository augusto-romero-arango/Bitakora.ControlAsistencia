using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.MarcarDescansoAcumuladoDeJornadaFunction.CommandHandler;

public partial class MarcarDescansoAcumuladoDeJornadaCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.MarcarDescansoAcumuladoDeJornadaFunction.CommandHandler.MarcarDescansoAcumuladoDeJornadaCommandHandlerMensajes",
        typeof(MarcarDescansoAcumuladoDeJornadaCommandHandler).Assembly);
    public static class Mensajes
    {
        public static string JornadaNoEncontrada => ResourceManager.GetString(nameof(JornadaNoEncontrada))!;
        public static string JornadaIdenticaExistente => ResourceManager.GetString(nameof(JornadaIdenticaExistente))!;
    }
}
