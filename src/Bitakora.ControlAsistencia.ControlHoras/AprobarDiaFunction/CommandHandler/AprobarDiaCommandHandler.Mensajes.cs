using System.Resources;

namespace Bitakora.ControlAsistencia.ControlHoras.AprobarDiaFunction.CommandHandler;

// public y no internal: ControlHoras no declara InternalsVisibleTo hacia sus tests, que afirman
// estos mensajes.
public partial class AprobarDiaCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.ControlHoras.AprobarDiaFunction.CommandHandler.AprobarDiaCommandHandlerMensajes",
        typeof(AprobarDiaCommandHandler).Assembly);

    public static class Mensajes
    {
        // Payload vacio o incompleto.
        public static string ConflictosSinDecidir =>
            ResourceManager.GetString(nameof(ConflictosSinDecidir))!;

        public static string CodigoSedeNoCandidata =>
            ResourceManager.GetString(nameof(CodigoSedeNoCandidata))!;

        // Franja sin conflicto, o HoraInicioProgramada inexistente en el expediente.
        public static string DecisionParaFranjaInvalida =>
            ResourceManager.GetString(nameof(DecisionParaFranjaInvalida))!;

        public static string DiaYaAprobado =>
            ResourceManager.GetString(nameof(DiaYaAprobado))!;

        public static string MarcacionesAmbiguasSinResolver =>
            ResourceManager.GetString(nameof(MarcacionesAmbiguasSinResolver))!;
    }
}
