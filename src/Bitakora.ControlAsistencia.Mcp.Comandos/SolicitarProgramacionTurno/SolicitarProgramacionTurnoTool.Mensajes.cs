using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.SolicitarProgramacionTurno;

// MEF-ADR-0009: mensajes runtime de la tool en .resx separado. Las descripciones MCP de la tool y
// de sus parametros NO viven aqui: son atributos y exigen constantes de compilacion.
public partial class SolicitarProgramacionTurnoTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Comandos.SolicitarProgramacionTurno.SolicitarProgramacionTurnoToolMensajes",
        typeof(SolicitarProgramacionTurnoTool).Assembly);

    internal static class Mensajes
    {
        /// <summary>{0}: nombre del campo obligatorio en blanco.</summary>
        public static string CampoObligatorio =>
            ResourceManager.GetString(nameof(CampoObligatorio))!;

        /// <summary>{0}: nombre del campo; {1}: valor recibido.</summary>
        public static string FechaInvalida =>
            ResourceManager.GetString(nameof(FechaInvalida))!;

        public static string VentanaInvertida =>
            ResourceManager.GetString(nameof(VentanaInvertida))!;

        /// <summary>{0}: dias recibidos en la ventana invalida.</summary>
        public static string VentanaExcedeMaximo =>
            ResourceManager.GetString(nameof(VentanaExcedeMaximo))!;

        /// <summary>{0}: maximo de identificaciones admitido.</summary>
        public static string DemasiadasIdentificaciones =>
            ResourceManager.GetString(nameof(DemasiadasIdentificaciones))!;

        /// <summary>{0}: nombre de turno recibido; {1}: nombres disponibles en el catalogo.</summary>
        public static string TurnoNoExiste =>
            ResourceManager.GetString(nameof(TurnoNoExiste))!;

        /// <summary>{0}: codigo de sede recibido.</summary>
        public static string SedeNoExiste =>
            ResourceManager.GetString(nameof(SedeNoExiste))!;

        /// <summary>{0}: codigo de sede recibido.</summary>
        public static string SedeInactiva =>
            ResourceManager.GetString(nameof(SedeInactiva))!;

        /// <summary>{0}: cuerpo de la respuesta 400/404/409/5xx del dominio.</summary>
        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string ResultadoProgramacionSolicitada =>
            ResourceManager.GetString(nameof(ResultadoProgramacionSolicitada))!;

        public static string NotaVisibilidadEventual =>
            ResourceManager.GetString(nameof(NotaVisibilidadEventual))!;
    }
}
