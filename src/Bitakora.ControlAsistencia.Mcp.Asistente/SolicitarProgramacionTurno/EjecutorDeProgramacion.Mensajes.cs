using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;

internal static partial class EjecutorDeProgramacion
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno.EjecutorDeProgramacionMensajes",
        typeof(EjecutorDeProgramacion).Assembly);

    internal static class Mensajes
    {
        public static string NotaBorradorPorLista => ResourceManager.GetString(nameof(NotaBorradorPorLista))!;

        public static string NotaBorradorPorGrupo => ResourceManager.GetString(nameof(NotaBorradorPorGrupo))!;
    }
}
