using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.CancelarAusencia;

public partial class CancelarAusenciaTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Comandos.CancelarAusencia.CancelarAusenciaToolMensajes",
        typeof(CancelarAusenciaTool).Assembly);

    internal static class Mensajes
    {
        public static string CampoObligatorio =>
            ResourceManager.GetString(nameof(CampoObligatorio))!;

        public static string FechaInvalida =>
            ResourceManager.GetString(nameof(FechaInvalida))!;

        public static string PeriodoInvertido =>
            ResourceManager.GetString(nameof(PeriodoInvertido))!;

        public static string ColaboradorNoEncontrado =>
            ResourceManager.GetString(nameof(ColaboradorNoEncontrado))!;

        public static string SinAusenciasEnElPeriodo =>
            ResourceManager.GetString(nameof(SinAusenciasEnElPeriodo))!;

        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string ResultadoAusenciasCanceladas =>
            ResourceManager.GetString(nameof(ResultadoAusenciasCanceladas))!;
    }
}
