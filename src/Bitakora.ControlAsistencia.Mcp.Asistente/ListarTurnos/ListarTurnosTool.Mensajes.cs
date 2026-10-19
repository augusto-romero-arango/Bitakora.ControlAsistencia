using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ListarTurnos;

public partial class ListarTurnosTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.ListarTurnos.ListarTurnosToolMensajes",
        typeof(ListarTurnosTool).Assembly);

    internal static class Mensajes
    {
        /// <summary>{0}: siguienteCursor.</summary>
        public static string NotaHayMasTurnos => ResourceManager.GetString(nameof(NotaHayMasTurnos))!;

        public static string SinTurnos => ResourceManager.GetString(nameof(SinTurnos))!;

        /// <summary>{0}: codigo de la sede.</summary>
        public static string SinTurnosEnSede => ResourceManager.GetString(nameof(SinTurnosEnSede))!;

        /// <summary>{0}: codigo de la sede.</summary>
        public static string SedeNoExiste => ResourceManager.GetString(nameof(SedeNoExiste))!;

        /// <summary>{0}: cuerpo del fallo del backend.</summary>
        public static string RechazoDelDominio => ResourceManager.GetString(nameof(RechazoDelDominio))!;
    }
}
