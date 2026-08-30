using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.ConsultarProgramacion;

// MEF-ADR-0009: mensajes runtime de la tool en .resx separado. Las descripciones MCP de la tool y
// de sus parametros NO viven aqui: son atributos y exigen constantes de compilacion.
public partial class ConsultarProgramacionTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Consultas.ConsultarProgramacion.ConsultarProgramacionToolMensajes",
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

        /// <summary>{0}: dias mostrados, {1}: total de la respuesta.</summary>
        public static string NotaTruncado =>
            ResourceManager.GetString(nameof(NotaTruncado))!;
    }
}
