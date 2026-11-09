using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.AsignarModalidadAJornadaFunction.CommandHandler;

public partial class AsignarModalidadAJornadaCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.AsignarModalidadAJornadaFunction.CommandHandler.AsignarModalidadAJornadaCommandHandlerMensajes",
        typeof(AsignarModalidadAJornadaCommandHandler).Assembly);
    public static class Mensajes
    {
        public static string JornadaNoEncontrada => ResourceManager.GetString(nameof(JornadaNoEncontrada))!;
        public static string ModalidadDuplicada => ResourceManager.GetString(nameof(ModalidadDuplicada))!;
    }
}
