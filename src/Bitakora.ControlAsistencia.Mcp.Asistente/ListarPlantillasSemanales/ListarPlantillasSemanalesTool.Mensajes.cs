using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ListarPlantillasSemanales;

public partial class ListarPlantillasSemanalesTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.ListarPlantillasSemanales.ListarPlantillasSemanalesToolMensajes",
        typeof(ListarPlantillasSemanalesTool).Assembly);

    internal static class Mensajes
    {
        /// <summary>{0}: siguienteCursor.</summary>
        public static string NotaHayMasPlantillas => ResourceManager.GetString(nameof(NotaHayMasPlantillas))!;

        public static string SinPlantillas => ResourceManager.GetString(nameof(SinPlantillas))!;

        /// <summary>{0}: cuerpo del fallo del backend.</summary>
        public static string RechazoDelDominio => ResourceManager.GetString(nameof(RechazoDelDominio))!;
    }
}
