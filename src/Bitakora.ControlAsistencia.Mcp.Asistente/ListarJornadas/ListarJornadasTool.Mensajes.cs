using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ListarJornadas;

public partial class ListarJornadasTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.ListarJornadas.ListarJornadasToolMensajes",
        typeof(ListarJornadasTool).Assembly);

    internal static class Mensajes
    {
        public static string NotaMasJornadas =>
            ResourceManager.GetString(nameof(NotaMasJornadas))!;

        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

    }
}
