using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ListarSedes;

public partial class ListarSedesTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.ListarSedes.ListarSedesToolMensajes",
        typeof(ListarSedesTool).Assembly);

    internal static class Mensajes
    {
        /// <summary>{0}: siguienteCursor.</summary>
        public static string NotaHayMasSedes => ResourceManager.GetString(nameof(NotaHayMasSedes))!;

        public static string SinSedes => ResourceManager.GetString(nameof(SinSedes))!;

        /// <summary>{0}: cuerpo del fallo del backend.</summary>
        public static string RechazoDelDominio => ResourceManager.GetString(nameof(RechazoDelDominio))!;
    }
}
