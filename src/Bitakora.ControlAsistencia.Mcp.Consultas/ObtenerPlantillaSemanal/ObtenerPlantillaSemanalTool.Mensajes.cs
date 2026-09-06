using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.ObtenerPlantillaSemanal;

// MEF-ADR-0009: mensajes runtime de la tool en .resx separado. Las descripciones MCP de la tool y
// de sus parametros NO viven aqui: son atributos y exigen constantes de compilacion.
public partial class ObtenerPlantillaSemanalTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Consultas.ObtenerPlantillaSemanal.ObtenerPlantillaSemanalToolMensajes",
        typeof(ObtenerPlantillaSemanalTool).Assembly);

    internal static class Mensajes
    {
        /// <summary>{0}: nombre del campo obligatorio en blanco.</summary>
        public static string CampoObligatorio =>
            ResourceManager.GetString(nameof(CampoObligatorio))!;

        /// <summary>{0}: nombre de plantilla recibido; {1}: nombres disponibles en el catalogo.</summary>
        public static string PlantillaNoExiste =>
            ResourceManager.GetString(nameof(PlantillaNoExiste))!;

        /// <summary>{0}: el motivo crudo devuelto por ResolutorPlantillaPorNombre.LeerFalloAsync.</summary>
        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string SinTurno =>
            ResourceManager.GetString(nameof(SinTurno))!;

        /// <summary>{0}: nombre del turno, o su id cuando la ficha ya no existe en el catalogo.</summary>
        public static string TurnoRetirado =>
            ResourceManager.GetString(nameof(TurnoRetirado))!;

        /// <summary>{0}: nombre del turno.</summary>
        public static string TurnoIncompleto =>
            ResourceManager.GetString(nameof(TurnoIncompleto))!;
    }
}
