using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction.CommandHandler;

public partial class AgregarSubFranjaCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction.CommandHandler.AgregarSubFranjaCommandHandlerMensajes",
        typeof(AgregarSubFranjaCommandHandler).Assembly);

    // internal, no private: los tests lo afirman via InternalsVisibleTo.
    internal static class Mensajes
    {
        public static string TurnoNoEncontrado =>
            ResourceManager.GetString(nameof(TurnoNoEncontrado))!;

        public static string TurnoRetirado =>
            ResourceManager.GetString(nameof(TurnoRetirado))!;

        public static string TurnoEsDescanso =>
            ResourceManager.GetString(nameof(TurnoEsDescanso))!;

        public static string FranjaNoExiste =>
            ResourceManager.GetString(nameof(FranjaNoExiste))!;
    }
}
