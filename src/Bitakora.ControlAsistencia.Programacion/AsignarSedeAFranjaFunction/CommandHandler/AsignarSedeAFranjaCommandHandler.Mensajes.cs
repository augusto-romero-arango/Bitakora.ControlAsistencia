using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.AsignarSedeAFranjaFunction.CommandHandler;

public partial class AsignarSedeAFranjaCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.AsignarSedeAFranjaFunction.CommandHandler.AsignarSedeAFranjaCommandHandlerMensajes",
        typeof(AsignarSedeAFranjaCommandHandler).Assembly);

    // internal, no private: los tests lo afirman via InternalsVisibleTo.
    internal static class Mensajes
    {
        public static string TurnoNoEncontrado =>
            ResourceManager.GetString(nameof(TurnoNoEncontrado))!;

        public static string TurnoRetirado =>
            ResourceManager.GetString(nameof(TurnoRetirado))!;

        public static string FranjaNoExiste =>
            ResourceManager.GetString(nameof(FranjaNoExiste))!;

        public static string FranjaSinSede =>
            ResourceManager.GetString(nameof(FranjaSinSede))!;
    }
}
