using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.BuscarSedes;

public partial class BuscarSedesTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.BuscarSedes.BuscarSedesToolMensajes",
        typeof(BuscarSedesTool).Assembly);

    internal static class Mensajes
    {
        public static string FaltaCriterio => ResourceManager.GetString(nameof(FaltaCriterio))!;

        /// <summary>{0}: criterios usados.</summary>
        public static string SinCoincidencias => ResourceManager.GetString(nameof(SinCoincidencias))!;

        public static string NotaTruncado => ResourceManager.GetString(nameof(NotaTruncado))!;

        /// <summary>{0}: palabras coincidentes.</summary>
        public static string CoincidePorNombre => ResourceManager.GetString(nameof(CoincidePorNombre))!;

        /// <summary>{0}: codigo de la sede.</summary>
        public static string CoincidePorCodigo => ResourceManager.GetString(nameof(CoincidePorCodigo))!;

        /// <summary>{0}: palabras coincidentes.</summary>
        public static string CoincidePorCiudad => ResourceManager.GetString(nameof(CoincidePorCiudad))!;

        /// <summary>{0}: palabras coincidentes.</summary>
        public static string CoincidePorDireccion => ResourceManager.GetString(nameof(CoincidePorDireccion))!;

        /// <summary>{0}: palabra pedida; {1}: palabra encontrada.</summary>
        public static string PalabraAproximada => ResourceManager.GetString(nameof(PalabraAproximada))!;

        public static string CriterioNombre => ResourceManager.GetString(nameof(CriterioNombre))!;

        public static string CriterioCiudad => ResourceManager.GetString(nameof(CriterioCiudad))!;

        public static string CriterioDireccion => ResourceManager.GetString(nameof(CriterioDireccion))!;
    }
}
