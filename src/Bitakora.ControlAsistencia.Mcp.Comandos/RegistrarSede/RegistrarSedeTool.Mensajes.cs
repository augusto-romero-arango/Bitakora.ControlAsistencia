using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.RegistrarSede;

// MEF-ADR-0009: mensajes runtime de la tool en .resx separado. Las descripciones MCP de la tool y
// de sus parametros NO viven aqui: son atributos y exigen constantes de compilacion.
public partial class RegistrarSedeTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Comandos.RegistrarSede.RegistrarSedeToolMensajes",
        typeof(RegistrarSedeTool).Assembly);

    internal static class Mensajes
    {
        /// <summary>{0}: nombre del campo obligatorio en blanco.</summary>
        public static string CampoObligatorio =>
            ResourceManager.GetString(nameof(CampoObligatorio))!;

        /// <summary>{0}: cuerpo de la respuesta 400/409 del dominio.</summary>
        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string NotaVisibilidadEventual =>
            ResourceManager.GetString(nameof(NotaVisibilidadEventual))!;
    }
}
