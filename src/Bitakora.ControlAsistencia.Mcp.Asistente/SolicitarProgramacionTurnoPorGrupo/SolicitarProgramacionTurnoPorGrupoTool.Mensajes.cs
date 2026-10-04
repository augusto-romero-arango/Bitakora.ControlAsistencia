using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurnoPorGrupo;

public partial class SolicitarProgramacionTurnoPorGrupoTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurnoPorGrupo.SolicitarProgramacionTurnoPorGrupoToolMensajes",
        typeof(SolicitarProgramacionTurnoPorGrupoTool).Assembly);

    internal static class Mensajes
    {
        /// <summary>{0}: nombre del campo obligatorio en blanco.</summary>
        public static string CampoObligatorio => ResourceManager.GetString(nameof(CampoObligatorio))!;

        /// <summary>{0}: nombre del campo; {1}: valor recibido.</summary>
        public static string FechaInvalida => ResourceManager.GetString(nameof(FechaInvalida))!;

        public static string VentanaInvertida => ResourceManager.GetString(nameof(VentanaInvertida))!;

        /// <summary>{0}: dias recibidos en la ventana invalida.</summary>
        public static string VentanaExcedeMaximo => ResourceManager.GetString(nameof(VentanaExcedeMaximo))!;

        public static string SelectorObligatorio => ResourceManager.GetString(nameof(SelectorObligatorio))!;

        /// <summary>{0}: el par recibido que no tiene forma categoria:valor.</summary>
        public static string EtiquetaMalFormada => ResourceManager.GetString(nameof(EtiquetaMalFormada))!;

        /// <summary>{0}: nombre de turno recibido; {1}: nombres disponibles en el catalogo.</summary>
        public static string TurnoNoExiste => ResourceManager.GetString(nameof(TurnoNoExiste))!;

        /// <summary>{0}: codigo de sede de programacion recibido.</summary>
        public static string SedeNoExiste => ResourceManager.GetString(nameof(SedeNoExiste))!;

        /// <summary>{0}: codigo de sede de programacion recibido.</summary>
        public static string SedeInactiva => ResourceManager.GetString(nameof(SedeInactiva))!;

        /// <summary>{0}: codigo de la sede del selector recibido.</summary>
        public static string SedeDelSelectorNoExiste => ResourceManager.GetString(nameof(SedeDelSelectorNoExiste))!;

        /// <summary>{0}: codigo de la sede del selector recibido.</summary>
        public static string SedeDelSelectorInactiva => ResourceManager.GetString(nameof(SedeDelSelectorInactiva))!;

        /// <summary>{0}: cuerpo de la respuesta 400/404/409/5xx del dominio.</summary>
        public static string RechazoDelDominio => ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string ResultadoProgramacionSolicitada =>
            ResourceManager.GetString(nameof(ResultadoProgramacionSolicitada))!;

        public static string AvisoSinSede =>
            ResourceManager.GetString(nameof(AvisoSinSede))!;

        /// <summary>{0}: codigo de la sede del colaborador.</summary>
        public static string AvisoSedeInactiva =>
            ResourceManager.GetString(nameof(AvisoSedeInactiva))!;

        /// <summary>{0}: codigo de la sede del colaborador.</summary>
        public static string AvisoSedeNoExiste =>
            ResourceManager.GetString(nameof(AvisoSedeNoExiste))!;

        public static string NotaVisibilidadEventual => ResourceManager.GetString(nameof(NotaVisibilidadEventual))!;
    }
}
