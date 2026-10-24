using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.CrearTurnoDeHorarioAbierto;

public partial class CrearTurnoDeHorarioAbiertoTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.CrearTurnoDeHorarioAbierto.CrearTurnoDeHorarioAbiertoToolMensajes",
        typeof(CrearTurnoDeHorarioAbiertoTool).Assembly);

    internal static class Mensajes
    {
        public static string CampoObligatorio => ResourceManager.GetString(nameof(CampoObligatorio))!;

        public static string DuracionInvalida => ResourceManager.GetString(nameof(DuracionInvalida))!;

        public static string RechazoDelDominio => ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string ResultadoTurnoCreado => ResourceManager.GetString(nameof(ResultadoTurnoCreado))!;

        public static string EntraEntre => ResourceManager.GetString(nameof(EntraEntre))!;

        public static string TurnoFijo => ResourceManager.GetString(nameof(TurnoFijo))!;

        public static string NotaVisibilidadEventual => ResourceManager.GetString(nameof(NotaVisibilidadEventual))!;
    }
}
