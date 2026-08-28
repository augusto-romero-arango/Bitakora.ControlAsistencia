using System.Resources;

namespace Bitakora.ControlAsistencia.Sedes.RetirarCentroDeCostosFunction.CommandHandler;

public partial class RetirarCentroDeCostosCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Sedes.RetirarCentroDeCostosFunction.CommandHandler.RetirarCentroDeCostosCommandHandlerMensajes",
        typeof(RetirarCentroDeCostosCommandHandler).Assembly);

    // internal, no private: los tests afirman los mensajes del 404/409 via InternalsVisibleTo.
    internal static class Mensajes
    {
        public static string SedeNoEncontrada =>
            ResourceManager.GetString(nameof(SedeNoEncontrada))!;

        public static string SinCentroDeCostosVigente =>
            ResourceManager.GetString(nameof(SinCentroDeCostosVigente))!;
    }
}
