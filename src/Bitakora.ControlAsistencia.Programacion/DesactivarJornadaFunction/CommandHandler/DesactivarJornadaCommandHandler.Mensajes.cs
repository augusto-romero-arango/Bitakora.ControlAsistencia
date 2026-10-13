using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.DesactivarJornadaFunction.CommandHandler;

public partial class DesactivarJornadaCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.DesactivarJornadaFunction.CommandHandler.DesactivarJornadaCommandHandlerMensajes",
        typeof(DesactivarJornadaCommandHandler).Assembly);

    public static class Mensajes
    {
        public static string JornadaNoEncontrada => ResourceManager.GetString(nameof(JornadaNoEncontrada))!;
        public static string JornadaPredeterminada => ResourceManager.GetString(nameof(JornadaPredeterminada))!;
        public static string JornadaEnUsoPorPlantillas => ResourceManager.GetString(nameof(JornadaEnUsoPorPlantillas))!;
    }
}
