using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;

// No reusa SolicitarProgramacionTurnoTool.Mensajes: el VO protege su propio invariante, independiente
// de la validacion previa que hace la tool.
public partial class VentanaDeProgramacion
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno.VentanaDeProgramacionMensajes",
        typeof(VentanaDeProgramacion).Assembly);

    internal static class Mensajes
    {
        public static string VentanaInvertida =>
            ResourceManager.GetString(nameof(VentanaInvertida))!;

        /// <summary>{0}: dias recibidos en la ventana invalida.</summary>
        public static string VentanaExcedeMaximo =>
            ResourceManager.GetString(nameof(VentanaExcedeMaximo))!;
    }
}
