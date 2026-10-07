using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction.CommandHandler;

public partial class CrearJornadaCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction.CommandHandler.CrearJornadaCommandHandlerMensajes",
        typeof(CrearJornadaCommandHandler).Assembly);
    public static class Mensajes
    {
        public static string JornadaYaExiste => ResourceManager.GetString(nameof(JornadaYaExiste))!;
    }
}
