using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.BuscarColaboradores;

// MEF-ADR-0009: mensajes runtime de la tool en .resx separado. Las descripciones MCP de la tool y
// de sus parametros NO viven aqui: son atributos y exigen constantes de compilacion.
public partial class BuscarColaboradoresTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Consultas.BuscarColaboradores.BuscarColaboradoresToolMensajes",
        typeof(BuscarColaboradoresTool).Assembly);

    internal static class Mensajes
    {
        public static string FaltaCriterio =>
            ResourceManager.GetString(nameof(FaltaCriterio))!;

        /// <summary>{0}: colaboradores mostrados, {1}: total recibido del dominio.</summary>
        public static string NotaTruncado =>
            ResourceManager.GetString(nameof(NotaTruncado))!;

        /// <summary>{0}: cuerpo de la respuesta 400/422 del dominio.</summary>
        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;
    }
}
