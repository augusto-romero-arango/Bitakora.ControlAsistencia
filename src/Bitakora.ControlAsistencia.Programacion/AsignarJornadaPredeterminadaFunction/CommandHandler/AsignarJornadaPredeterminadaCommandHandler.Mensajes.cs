using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.AsignarJornadaPredeterminadaFunction.CommandHandler;

public partial class AsignarJornadaPredeterminadaCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.AsignarJornadaPredeterminadaFunction.CommandHandler.AsignarJornadaPredeterminadaCommandHandlerMensajes",
        typeof(AsignarJornadaPredeterminadaCommandHandler).Assembly);
    public static class Mensajes
    {
        public static string JornadaNoEncontrada => ResourceManager.GetString(nameof(JornadaNoEncontrada))!;
    }
}
