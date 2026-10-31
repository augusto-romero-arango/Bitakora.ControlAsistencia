using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarProgramacion;

// MEF-ADR-0009: mensajes runtime de la tool en .resx separado. Las descripciones MCP de la tool y
// de sus parametros NO viven aqui: son atributos y exigen constantes de compilacion.
public partial class ConsultarProgramacionTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarProgramacion.ConsultarProgramacionToolMensajes",
        typeof(ConsultarProgramacionTool).Assembly);

    internal static class Mensajes
    {
        /// <summary>{0}: nombre del parametro, {1}: valor recibido.</summary>
        public static string FechaInvalida =>
            ResourceManager.GetString(nameof(FechaInvalida))!;

        public static string DesdePosteriorAHasta =>
            ResourceManager.GetString(nameof(DesdePosteriorAHasta))!;

        /// <summary>{0}: cuerpo de la respuesta 400/422 del dominio.</summary>
        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string NotaRecorte =>
            ResourceManager.GetString(nameof(NotaRecorte))!;

        public static string NotaMasDias =>
            ResourceManager.GetString(nameof(NotaMasDias))!;

        /// <summary>{0}: duracion.</summary>
        public static string HorarioAbierto => ResourceManager.GetString(nameof(HorarioAbierto))!;

        /// <summary>{0}: codigo de la sede de trabajo.</summary>
        public static string SedeTrabajoNoExiste =>
            ResourceManager.GetString(nameof(SedeTrabajoNoExiste))!;

        /// <summary>{0}: criterio, {1}: periodo.</summary>
        public static string GrupoSinColaboradores =>
            ResourceManager.GetString(nameof(GrupoSinColaboradores))!;

        /// <summary>{0}: periodo, {1}: criterio (puede ir vacio).</summary>
        public static string SinProgramacion =>
            ResourceManager.GetString(nameof(SinProgramacion))!;

        /// <summary>{0}: codigo de sede.</summary>
        public static string CriterioSede =>
            ResourceManager.GetString(nameof(CriterioSede))!;

        /// <summary>{0}: pares categoria:valor.</summary>
        public static string CriterioEtiquetas =>
            ResourceManager.GetString(nameof(CriterioEtiquetas))!;

        /// <summary>{0}: codigos de colaborador.</summary>
        public static string CriterioCodigos =>
            ResourceManager.GetString(nameof(CriterioCodigos))!;

        /// <summary>{0}: codigo de la sede de trabajo.</summary>
        public static string CriterioSedeTrabajo =>
            ResourceManager.GetString(nameof(CriterioSedeTrabajo))!;
    }
}
