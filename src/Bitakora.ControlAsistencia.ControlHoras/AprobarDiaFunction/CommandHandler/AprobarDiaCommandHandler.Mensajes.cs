using System.Resources;

namespace Bitakora.ControlAsistencia.ControlHoras.AprobarDiaFunction.CommandHandler;

// Mensajes del handler en .resx separado (MEF-ADR-0009). public y no internal: ControlHoras no
// declara InternalsVisibleTo hacia sus tests, que afirman estos mensajes (mismo criterio que
// SedeDeMarcacionResueltaEventHandler.Mensajes).
public partial class AprobarDiaCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.ControlHoras.AprobarDiaFunction.CommandHandler.AprobarDiaCommandHandlerMensajes",
        typeof(AprobarDiaCommandHandler).Assembly);

    public static class Mensajes
    {
        // CA-3 (payload vacio o incompleto).
        public static string ConflictosSinDecidir =>
            ResourceManager.GetString(nameof(ConflictosSinDecidir))!;

        // CA-4.
        public static string CodigoSedeNoCandidata =>
            ResourceManager.GetString(nameof(CodigoSedeNoCandidata))!;

        // CA-5 (franja sin conflicto, o HoraInicioProgramada inexistente en el expediente).
        public static string DecisionParaFranjaInvalida =>
            ResourceManager.GetString(nameof(DecisionParaFranjaInvalida))!;

        // CA-6.
        public static string DiaYaAprobado =>
            ResourceManager.GetString(nameof(DiaYaAprobado))!;
    }
}
