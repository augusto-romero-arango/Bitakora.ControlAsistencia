using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.QuitarFranjaFunction.CommandHandler;

public partial class QuitarFranjaCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.QuitarFranjaFunction.CommandHandler.QuitarFranjaCommandHandlerMensajes",
        typeof(QuitarFranjaCommandHandler).Assembly);

    // internal, no private: los tests lo afirman via InternalsVisibleTo.
    internal static class Mensajes
    {
        public static string TurnoNoEncontrado =>
            ResourceManager.GetString(nameof(TurnoNoEncontrado))!;

        public static string TurnoRetirado =>
            ResourceManager.GetString(nameof(TurnoRetirado))!;

        public static string FranjaNoExiste =>
            ResourceManager.GetString(nameof(FranjaNoExiste))!;
    }
}
