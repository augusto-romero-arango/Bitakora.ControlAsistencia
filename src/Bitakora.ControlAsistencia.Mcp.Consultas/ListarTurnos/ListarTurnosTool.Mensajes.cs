using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.ListarTurnos;

// MEF-ADR-0009: mensajes runtime de la tool en .resx separado. Las descripciones MCP de la tool y
// de sus parametros NO viven aqui: son atributos y exigen constantes de compilacion.
public partial class ListarTurnosTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Consultas.ListarTurnos.ListarTurnosToolMensajes",
        typeof(ListarTurnosTool).Assembly);

    internal static class Mensajes
    {
        /// <summary>{0}: turnos mostrados, {1}: total tras el filtro.</summary>
        public static string NotaTruncado =>
            ResourceManager.GetString(nameof(NotaTruncado))!;
    }
}
