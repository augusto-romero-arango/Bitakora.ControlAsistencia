using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.QuitarModalidadDeJornadaFunction.CommandHandler;

public partial class QuitarModalidadDeJornadaCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.QuitarModalidadDeJornadaFunction.CommandHandler.QuitarModalidadDeJornadaCommandHandlerMensajes",
        typeof(QuitarModalidadDeJornadaCommandHandler).Assembly);
    public static class Mensajes
    {
        public static string JornadaNoEncontrada => ResourceManager.GetString(nameof(JornadaNoEncontrada))!;
        public static string LimitesDuplicadosSinModalidad => ResourceManager.GetString(nameof(LimitesDuplicadosSinModalidad))!;
    }
}
