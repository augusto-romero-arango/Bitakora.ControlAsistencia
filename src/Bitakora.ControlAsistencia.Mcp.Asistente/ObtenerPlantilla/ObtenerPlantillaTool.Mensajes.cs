using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ObtenerPlantilla;

public partial class ObtenerPlantillaTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.ObtenerPlantilla.ObtenerPlantillaToolMensajes",
        typeof(ObtenerPlantillaTool).Assembly);

    internal static class Mensajes
    {
        public static string PlantillaNoExiste => ResourceManager.GetString(nameof(PlantillaNoExiste))!;

        public static string RechazoDelDominio => ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string SinTurno => ResourceManager.GetString(nameof(SinTurno))!;

        public static string TurnoRetirado => ResourceManager.GetString(nameof(TurnoRetirado))!;

        public static string TurnoIncompleto => ResourceManager.GetString(nameof(TurnoIncompleto))!;

        public static string Descanso => ResourceManager.GetString(nameof(Descanso))!;

        public static string HorarioAbierto => ResourceManager.GetString(nameof(HorarioAbierto))!;

        public static string PlantillaIdInvalido => ResourceManager.GetString(nameof(PlantillaIdInvalido))!;

    }
}
