using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.SolicitarProgramacionTurno;

// MEF-ADR-0009: mensajes runtime del VO en .resx separado y co-localizado (regla del test-writer
// 6b, extendida a value objects). El VO no depende de SolicitarProgramacionTurnoTool.Mensajes: es
// su propio invariante, independiente de la validacion previa que hace la tool.
public partial class VentanaDeProgramacion
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Comandos.SolicitarProgramacionTurno.VentanaDeProgramacionMensajes",
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
