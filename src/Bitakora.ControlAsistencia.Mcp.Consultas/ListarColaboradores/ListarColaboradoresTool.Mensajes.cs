using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.ListarColaboradores;

// MEF-ADR-0009: mensajes runtime de la tool en .resx separado. Las descripciones MCP de la tool y
// de sus parametros NO viven aqui: son atributos y exigen constantes de compilacion.
public partial class ListarColaboradoresTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Consultas.ListarColaboradores.ListarColaboradoresToolMensajes",
        typeof(ListarColaboradoresTool).Assembly);

    internal static class Mensajes
    {
        /// <summary>{0}: identificacion pedida.</summary>
        public static string ColaboradorNoExiste =>
            ResourceManager.GetString(nameof(ColaboradorNoExiste))!;

        /// <summary>{0}: valor recibido de fecha_referencia.</summary>
        public static string FechaInvalida =>
            ResourceManager.GetString(nameof(FechaInvalida))!;

        /// <summary>{0}: cuerpo de la respuesta 400/422 del dominio.</summary>
        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        /// <summary>{0}: colaboradores mostrados, {1}: total recibido del dominio.</summary>
        public static string NotaTruncado =>
            ResourceManager.GetString(nameof(NotaTruncado))!;
    }
}
