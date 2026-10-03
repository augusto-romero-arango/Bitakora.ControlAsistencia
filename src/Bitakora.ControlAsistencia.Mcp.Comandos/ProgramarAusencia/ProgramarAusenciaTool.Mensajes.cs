using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.ProgramarAusencia;

public partial class ProgramarAusenciaTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Comandos.ProgramarAusencia.ProgramarAusenciaToolMensajes",
        typeof(ProgramarAusenciaTool).Assembly);

    internal static class Mensajes
    {
        public static string CampoObligatorio =>
            ResourceManager.GetString(nameof(CampoObligatorio))!;

        public static string FechaInvalida =>
            ResourceManager.GetString(nameof(FechaInvalida))!;

        public static string PeriodoInvertido =>
            ResourceManager.GetString(nameof(PeriodoInvertido))!;

        public static string MotivoInvalido =>
            ResourceManager.GetString(nameof(MotivoInvalido))!;

        public static string ColaboradorNoEncontrado =>
            ResourceManager.GetString(nameof(ColaboradorNoEncontrado))!;

        public static string SinDiasVinculados =>
            ResourceManager.GetString(nameof(SinDiasVinculados))!;

        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string ResultadoAusenciaRegistrada =>
            ResourceManager.GetString(nameof(ResultadoAusenciaRegistrada))!;
    }
}
