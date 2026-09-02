using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.RegistrarColaborador;

// MEF-ADR-0009: mensajes runtime de la tool en .resx separado. Las descripciones MCP de la tool y
// de sus parametros NO viven aqui: son atributos y exigen constantes de compilacion.
public partial class RegistrarColaboradorTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Comandos.RegistrarColaborador.RegistrarColaboradorToolMensajes",
        typeof(RegistrarColaboradorTool).Assembly);

    internal static class Mensajes
    {
        public static string ResultadoColaboradorRegistrado =>
            ResourceManager.GetString(nameof(ResultadoColaboradorRegistrado))!;

        /// <summary>{0}: nombre del campo obligatorio en blanco.</summary>
        public static string CampoObligatorio =>
            ResourceManager.GetString(nameof(CampoObligatorio))!;

        /// <summary>{0}: valor recibido de fecha_inicio.</summary>
        public static string FechaInvalida =>
            ResourceManager.GetString(nameof(FechaInvalida))!;

        /// <summary>{0}: cuerpo de la respuesta 400/409 del dominio.</summary>
        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string NotaVisibilidadEventual =>
            ResourceManager.GetString(nameof(NotaVisibilidadEventual))!;
    }
}
