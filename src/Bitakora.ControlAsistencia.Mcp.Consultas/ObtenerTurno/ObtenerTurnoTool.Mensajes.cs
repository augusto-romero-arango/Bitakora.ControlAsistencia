using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.ObtenerTurno;

// MEF-ADR-0009: mensajes runtime de la tool en .resx separado. Las descripciones MCP de la tool y
// de sus parametros NO viven aqui: son atributos y exigen constantes de compilacion.
public partial class ObtenerTurnoTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Consultas.ObtenerTurno.ObtenerTurnoToolMensajes",
        typeof(ObtenerTurnoTool).Assembly);

    internal static class Mensajes
    {
        /// <summary>{0}: id pedido.</summary>
        public static string TurnoNoExiste =>
            ResourceManager.GetString(nameof(TurnoNoExiste))!;
    }
}
