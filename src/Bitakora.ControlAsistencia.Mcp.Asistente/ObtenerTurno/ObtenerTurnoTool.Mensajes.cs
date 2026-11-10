using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ObtenerTurno;

public partial class ObtenerTurnoTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.ObtenerTurno.ObtenerTurnoToolMensajes",
        typeof(ObtenerTurnoTool).Assembly);

    internal static class Mensajes
    {
        /// <summary>{0}: duracion de la ficha.</summary>
        public static string HorarioAbierto => ResourceManager.GetString(nameof(HorarioAbierto))!;

        /// <summary>{0}: valor recibido en turno_id.</summary>
        public static string TurnoIdInvalido =>
            ResourceManager.GetString(nameof(TurnoIdInvalido))!;

        /// <summary>{0}: id pedido.</summary>
        public static string TurnoNoExiste =>
            ResourceManager.GetString(nameof(TurnoNoExiste))!;

        /// <summary>{0}: apertura, {1}: cierre, {2}: amplitud.</summary>
        public static string EntraEntre =>
            ResourceManager.GetString(nameof(EntraEntre))!;
    }
}
