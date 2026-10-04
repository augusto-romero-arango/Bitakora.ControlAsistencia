using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AplicarPlantillaSemanalPorGrupo;

public partial class AplicarPlantillaSemanalPorGrupoTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.AplicarPlantillaSemanalPorGrupo.AplicarPlantillaSemanalPorGrupoToolMensajes",
        typeof(AplicarPlantillaSemanalPorGrupoTool).Assembly);

    internal static class Mensajes
    {
        public static string CampoObligatorio => ResourceManager.GetString(nameof(CampoObligatorio))!;
        public static string FechaInvalida => ResourceManager.GetString(nameof(FechaInvalida))!;
        public static string VentanaInvertida => ResourceManager.GetString(nameof(VentanaInvertida))!;
        public static string VentanaExcedeMaximo => ResourceManager.GetString(nameof(VentanaExcedeMaximo))!;
        public static string SelectorObligatorio => ResourceManager.GetString(nameof(SelectorObligatorio))!;
        public static string EtiquetaMalFormada => ResourceManager.GetString(nameof(EtiquetaMalFormada))!;
        public static string PlantillaNoExiste => ResourceManager.GetString(nameof(PlantillaNoExiste))!;
        public static string PlantillaIncompleta => ResourceManager.GetString(nameof(PlantillaIncompleta))!;
        public static string PlantillaConTurnoNoProgramable =>
            ResourceManager.GetString(nameof(PlantillaConTurnoNoProgramable))!;
        public static string SedeNoExiste => ResourceManager.GetString(nameof(SedeNoExiste))!;
        public static string SedeInactiva => ResourceManager.GetString(nameof(SedeInactiva))!;
        public static string SedeDelSelectorNoExiste => ResourceManager.GetString(nameof(SedeDelSelectorNoExiste))!;
        public static string SedeDelSelectorInactiva => ResourceManager.GetString(nameof(SedeDelSelectorInactiva))!;
        public static string RechazoDelDominio => ResourceManager.GetString(nameof(RechazoDelDominio))!;
        public static string ResultadoPlantillaAplicada => ResourceManager.GetString(nameof(ResultadoPlantillaAplicada))!;
        public static string NotaVisibilidadEventual => ResourceManager.GetString(nameof(NotaVisibilidadEventual))!;
        public static string AvisoSinSede => ResourceManager.GetString(nameof(AvisoSinSede))!;
        public static string AvisoSedeInactiva => ResourceManager.GetString(nameof(AvisoSedeInactiva))!;
        public static string AvisoSedeNoExiste => ResourceManager.GetString(nameof(AvisoSedeNoExiste))!;
    }
}
