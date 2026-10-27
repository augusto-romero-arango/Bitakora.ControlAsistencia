using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.BuscarPlantillas;

public partial class BuscarPlantillasTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.BuscarPlantillas.BuscarPlantillasToolMensajes",
        typeof(BuscarPlantillasTool).Assembly);

    internal static class Mensajes
    {
        public static string FaltaCriterio => ResourceManager.GetString(nameof(FaltaCriterio))!;

        public static string DiasInvalidos => ResourceManager.GetString(nameof(DiasInvalidos))!;

        public static string EstadoDesconocido => ResourceManager.GetString(nameof(EstadoDesconocido))!;

        public static string SinCoincidencias => ResourceManager.GetString(nameof(SinCoincidencias))!;

        public static string NotaTruncado => ResourceManager.GetString(nameof(NotaTruncado))!;

        public static string CoincidePorNombre => ResourceManager.GetString(nameof(CoincidePorNombre))!;

        public static string CoincidePorTurno => ResourceManager.GetString(nameof(CoincidePorTurno))!;

        public static string CoincidePorDias => ResourceManager.GetString(nameof(CoincidePorDias))!;

        public static string CoincidePorIncompleta => ResourceManager.GetString(nameof(CoincidePorIncompleta))!;

        public static string CoincidePorConAdvertencias => ResourceManager.GetString(nameof(CoincidePorConAdvertencias))!;

        public static string PalabraAproximada => ResourceManager.GetString(nameof(PalabraAproximada))!;

        public static string CriterioNombre => ResourceManager.GetString(nameof(CriterioNombre))!;

        public static string CriterioTurno => ResourceManager.GetString(nameof(CriterioTurno))!;

        public static string CriterioDias => ResourceManager.GetString(nameof(CriterioDias))!;

        public static string CriterioEstado => ResourceManager.GetString(nameof(CriterioEstado))!;

    }
}
