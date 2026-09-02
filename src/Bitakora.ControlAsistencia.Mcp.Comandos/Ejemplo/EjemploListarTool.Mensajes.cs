using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Ejemplo;

public partial class EjemploListarTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Comandos.Ejemplo.EjemploListarToolMensajes",
        typeof(EjemploListarTool).Assembly);

    internal static class Mensajes
    {
        /// <summary>{0}: elementos mostrados, {1}: total tras el filtro.</summary>
        public static string NotaTruncado => ResourceManager.GetString(nameof(NotaTruncado))!;

        /// <summary>{0}: largo maximo permitido.</summary>
        public static string ErrorFiltroDemasiadoLargo => ResourceManager.GetString(nameof(ErrorFiltroDemasiadoLargo))!;
    }
}
