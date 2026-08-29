using System.Resources;

namespace Bitakora.ControlAsistencia.ControlHoras.AprobarDiaFunction.CommandHandler;

// Mensajes del handler en .resx separado (MEF-ADR-0009).
// public y no internal: ControlHoras (Function App) no declara InternalsVisibleTo hacia sus tests,
// que afirman estos mensajes (mismo criterio que SedeDeMarcacionResueltaEventHandler.Mensajes).
public partial class AprobarDiaCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.ControlHoras.AprobarDiaFunction.CommandHandler.AprobarDiaCommandHandlerMensajes",
        typeof(AprobarDiaCommandHandler).Assembly);

    public static class Mensajes
    {
        // CA-3: el dia tiene franjas en conflicto de sede sin decidir (payload vacio o incompleto).
        public static string ConflictosSinDecidir =>
            ResourceManager.GetString(nameof(ConflictosSinDecidir))!;

        // CA-4: el CodigoSede decidido no esta entre las candidatas de esa franja.
        public static string CodigoSedeNoCandidata =>
            ResourceManager.GetString(nameof(CodigoSedeNoCandidata))!;

        // CA-5: la decision apunta a una franja sin conflicto o a una HoraInicioProgramada
        // inexistente en el expediente.
        public static string DecisionParaFranjaInvalida =>
            ResourceManager.GetString(nameof(DecisionParaFranjaInvalida))!;

        // CA-6: el dia ya fue aprobado -- las aprobaciones son definitivas.
        public static string DiaYaAprobado =>
            ResourceManager.GetString(nameof(DiaYaAprobado))!;
    }
}
