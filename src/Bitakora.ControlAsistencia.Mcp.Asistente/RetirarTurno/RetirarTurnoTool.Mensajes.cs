using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.RetirarTurno;

public partial class RetirarTurnoTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.RetirarTurno.RetirarTurnoToolMensajes",
        typeof(RetirarTurnoTool).Assembly);

    internal static class Mensajes
    {
        /// <summary>{0}: valor recibido en turno_id.</summary>
        public static string TurnoIdInvalido =>
            ResourceManager.GetString(nameof(TurnoIdInvalido))!;

        /// <summary>{0}: id de turno recibido.</summary>
        public static string TurnoNoExiste =>
            ResourceManager.GetString(nameof(TurnoNoExiste))!;

        /// <summary>{0}: cuerpo de la respuesta 404/409/5xx del dominio.</summary>
        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string ResultadoTurnoRetirado =>
            ResourceManager.GetString(nameof(ResultadoTurnoRetirado))!;

        public static string NotaVisibilidadEventual =>
            ResourceManager.GetString(nameof(NotaVisibilidadEventual))!;
    }
}
