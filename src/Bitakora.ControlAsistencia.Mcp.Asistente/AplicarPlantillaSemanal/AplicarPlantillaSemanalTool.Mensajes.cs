using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AplicarPlantillaSemanal;

public partial class AplicarPlantillaSemanalTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.AplicarPlantillaSemanal.AplicarPlantillaSemanalToolMensajes",
        typeof(AplicarPlantillaSemanalTool).Assembly);

    internal static class Mensajes
    {
        public static string CampoObligatorio => ResourceManager.GetString(nameof(CampoObligatorio))!;
        public static string FechaInvalida => ResourceManager.GetString(nameof(FechaInvalida))!;
        public static string VentanaInvertida => ResourceManager.GetString(nameof(VentanaInvertida))!;
        public static string VentanaExcedeMaximo => ResourceManager.GetString(nameof(VentanaExcedeMaximo))!;

        /// <summary>{0}: nombre recibido; {1}: nombres disponibles.</summary>
        public static string PlantillaNoExiste => ResourceManager.GetString(nameof(PlantillaNoExiste))!;

        /// <summary>{0}: nombre de la plantilla.</summary>
        public static string PlantillaIncompleta => ResourceManager.GetString(nameof(PlantillaIncompleta))!;

        /// <summary>{0}: nombre de la plantilla.</summary>
        public static string PlantillaConTurnoNoProgramable =>
            ResourceManager.GetString(nameof(PlantillaConTurnoNoProgramable))!;
    }
}
