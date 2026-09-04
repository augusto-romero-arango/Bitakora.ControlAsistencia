using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.AgregarFranjaFunction.CommandHandler;

public partial class AgregarFranjaCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.AgregarFranjaFunction.CommandHandler.AgregarFranjaCommandHandlerMensajes",
        typeof(AgregarFranjaCommandHandler).Assembly);

    // internal, no private: los tests lo afirman via InternalsVisibleTo.
    internal static class Mensajes
    {
        public static string TurnoNoEncontrado =>
            ResourceManager.GetString(nameof(TurnoNoEncontrado))!;

        public static string TurnoRetirado =>
            ResourceManager.GetString(nameof(TurnoRetirado))!;

        public static string TurnoEsDescanso =>
            ResourceManager.GetString(nameof(TurnoEsDescanso))!;

        public static string FranjaSeSolapa =>
            ResourceManager.GetString(nameof(FranjaSeSolapa))!;
    }
}
