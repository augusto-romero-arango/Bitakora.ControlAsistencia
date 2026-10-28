using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AplicarPlantilla;

public partial class AplicarPlantillaTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.AplicarPlantilla.AplicarPlantillaToolMensajes",
        typeof(AplicarPlantillaTool).Assembly);

    internal static class Mensajes
    {
        public static string CampoObligatorio => ResourceManager.GetString(nameof(CampoObligatorio))!;
        public static string FechaInvalida => ResourceManager.GetString(nameof(FechaInvalida))!;
        public static string FechaInicioPosteriorAHasta => ResourceManager.GetString(nameof(FechaInicioPosteriorAHasta))!;
        public static string VentanaInvertida => ResourceManager.GetString(nameof(VentanaInvertida))!;
        public static string VentanaExcedeMaximo => ResourceManager.GetString(nameof(VentanaExcedeMaximo))!;

        /// <summary>{0}: id recibido.</summary>
        public static string PlantillaNoExiste => ResourceManager.GetString(nameof(PlantillaNoExiste))!;

        /// <summary>{0}: valor de plantilla_id recibido.</summary>
        public static string PlantillaIdInvalido => ResourceManager.GetString(nameof(PlantillaIdInvalido))!;

        /// <summary>{0}: nombre de la plantilla.</summary>
        public static string PlantillaIncompleta => ResourceManager.GetString(nameof(PlantillaIncompleta))!;

        /// <summary>{0}: nombre de la plantilla.</summary>
        public static string PlantillaConTurnoNoProgramable =>
            ResourceManager.GetString(nameof(PlantillaConTurnoNoProgramable))!;

        public static string SedeNoExiste => ResourceManager.GetString(nameof(SedeNoExiste))!;
        public static string SedeInactiva => ResourceManager.GetString(nameof(SedeInactiva))!;
        public static string RechazoDelDominio => ResourceManager.GetString(nameof(RechazoDelDominio))!;
        public static string ResultadoPlantillaAplicada => ResourceManager.GetString(nameof(ResultadoPlantillaAplicada))!;
        public static string NotaVisibilidadEventual => ResourceManager.GetString(nameof(NotaVisibilidadEventual))!;
        public static string AvisoSinSede => ResourceManager.GetString(nameof(AvisoSinSede))!;
        public static string AvisoSedeInactiva => ResourceManager.GetString(nameof(AvisoSedeInactiva))!;
        public static string AvisoSedeNoExiste => ResourceManager.GetString(nameof(AvisoSedeNoExiste))!;
    }
}
