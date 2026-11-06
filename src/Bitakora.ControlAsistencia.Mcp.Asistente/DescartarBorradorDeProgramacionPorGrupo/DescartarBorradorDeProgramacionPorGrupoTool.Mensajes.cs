using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.DescartarBorradorDeProgramacionPorGrupo;

public partial class DescartarBorradorDeProgramacionPorGrupoTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.DescartarBorradorDeProgramacionPorGrupo.DescartarBorradorDeProgramacionPorGrupoToolMensajes",
        typeof(DescartarBorradorDeProgramacionPorGrupoTool).Assembly);

    internal static class Mensajes
    {
        public static string AlcanceObligatorio => ResourceManager.GetString(nameof(AlcanceObligatorio))!;
        public static string AlcanceExcluyente => ResourceManager.GetString(nameof(AlcanceExcluyente))!;
        public static string EtiquetaMalFormada => ResourceManager.GetString(nameof(EtiquetaMalFormada))!;
        public static string SedeDelSelectorNoExiste => ResourceManager.GetString(nameof(SedeDelSelectorNoExiste))!;
        public static string SedeDelSelectorInactiva => ResourceManager.GetString(nameof(SedeDelSelectorInactiva))!;
        public static string RechazoDelDominio => ResourceManager.GetString(nameof(RechazoDelDominio))!;
        public static string FechaInvalida => ResourceManager.GetString(nameof(FechaInvalida))!;
        public static string RangoIncompleto => ResourceManager.GetString(nameof(RangoIncompleto))!;
        public static string RangoInvertido => ResourceManager.GetString(nameof(RangoInvertido))!;
        public static string RangoExcedeElMaximo => ResourceManager.GetString(nameof(RangoExcedeElMaximo))!;
        public static string ResultadoBorradorDescartado => ResourceManager.GetString(nameof(ResultadoBorradorDescartado))!;
        public static string AlcanceTodoLoPendiente => ResourceManager.GetString(nameof(AlcanceTodoLoPendiente))!;
        public static string SinRango => ResourceManager.GetString(nameof(SinRango))!;
        public static string Nota => ResourceManager.GetString(nameof(Nota))!;
    }
}
