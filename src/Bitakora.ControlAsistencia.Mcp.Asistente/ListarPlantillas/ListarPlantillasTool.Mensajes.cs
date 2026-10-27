using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ListarPlantillas;

public partial class ListarPlantillasTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.ListarPlantillas.ListarPlantillasToolMensajes",
        typeof(ListarPlantillasTool).Assembly);

    internal static class Mensajes
    {
        public static string NotaHayMasPlantillas => ResourceManager.GetString(nameof(NotaHayMasPlantillas))!;

        public static string SinPlantillas => ResourceManager.GetString(nameof(SinPlantillas))!;

        public static string RechazoDelDominio => ResourceManager.GetString(nameof(RechazoDelDominio))!;

    }
}
