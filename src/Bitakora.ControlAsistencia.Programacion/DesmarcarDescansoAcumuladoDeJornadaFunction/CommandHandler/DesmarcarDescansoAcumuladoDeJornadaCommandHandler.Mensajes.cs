using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.DesmarcarDescansoAcumuladoDeJornadaFunction.CommandHandler;

public partial class DesmarcarDescansoAcumuladoDeJornadaCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.DesmarcarDescansoAcumuladoDeJornadaFunction.CommandHandler.DesmarcarDescansoAcumuladoDeJornadaCommandHandlerMensajes",
        typeof(DesmarcarDescansoAcumuladoDeJornadaCommandHandler).Assembly);
    public static class Mensajes
    {
        public static string JornadaNoEncontrada => ResourceManager.GetString(nameof(JornadaNoEncontrada))!;
        public static string JornadaIdenticaExistente => ResourceManager.GetString(nameof(JornadaIdenticaExistente))!;
    }
}
