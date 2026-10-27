using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AsignarJornadaAPlantillaSemanal;

public partial class AsignarJornadaAPlantillaSemanalTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.AsignarJornadaAPlantillaSemanal.AsignarJornadaAPlantillaSemanalToolMensajes",
        typeof(AsignarJornadaAPlantillaSemanalTool).Assembly);

    internal static class Mensajes
    {
        /// <summary>{0}: valor de jornada_id recibido.</summary>
        public static string JornadaIdInvalido =>
            ResourceManager.GetString(nameof(JornadaIdInvalido))!;

        /// <summary>{0}: id de plantilla recibido.</summary>
        public static string PlantillaNoExiste =>
            ResourceManager.GetString(nameof(PlantillaNoExiste))!;

        public static string JornadaNoExiste =>
            ResourceManager.GetString(nameof(JornadaNoExiste))!;

        /// <summary>{0}: cuerpo de la respuesta 404/409/5xx del dominio.</summary>
        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string ResultadoJornadaAsignada =>
            ResourceManager.GetString(nameof(ResultadoJornadaAsignada))!;

        public static string NotaAdvertenciasEventuales =>
            ResourceManager.GetString(nameof(NotaAdvertenciasEventuales))!;

        /// <summary>{0}: valor recibido en plantilla_id.</summary>
        public static string PlantillaIdInvalido =>
            ResourceManager.GetString(nameof(PlantillaIdInvalido))!;
    }
}
