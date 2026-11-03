using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.PublicarProgramacion;

public partial class PublicarProgramacionTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.PublicarProgramacion.PublicarProgramacionToolMensajes",
        typeof(PublicarProgramacionTool).Assembly);

    internal static class Mensajes
    {
        public static string CampoObligatorio =>
            ResourceManager.GetString(nameof(CampoObligatorio))!;

        public static string FechaInvalida =>
            ResourceManager.GetString(nameof(FechaInvalida))!;

        public static string PeriodoInvertido =>
            ResourceManager.GetString(nameof(PeriodoInvertido))!;

        public static string PeriodoExcedeElMaximo =>
            ResourceManager.GetString(nameof(PeriodoExcedeElMaximo))!;

        public static string ColaboradorNoEncontrado =>
            ResourceManager.GetString(nameof(ColaboradorNoEncontrado))!;

        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string ResultadoProgramacionPublicada =>
            ResourceManager.GetString(nameof(ResultadoProgramacionPublicada))!;
    }
}
