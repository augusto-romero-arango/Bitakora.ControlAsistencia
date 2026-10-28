using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AsignarTurnoADiaDePlantilla;

public partial class AsignarTurnoADiaDePlantillaTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.AsignarTurnoADiaDePlantilla.AsignarTurnoADiaDePlantillaToolMensajes",
        typeof(AsignarTurnoADiaDePlantillaTool).Assembly);

    internal static class Mensajes
    {
        public static string CampoObligatorio =>
            ResourceManager.GetString(nameof(CampoObligatorio))!;

        public static string DiaInvalido =>
            ResourceManager.GetString(nameof(DiaInvalido))!;

        public static string PlantillaIdInvalido =>
            ResourceManager.GetString(nameof(PlantillaIdInvalido))!;

        public static string TurnoIdInvalido =>
            ResourceManager.GetString(nameof(TurnoIdInvalido))!;

        public static string PlantillaNoExiste =>
            ResourceManager.GetString(nameof(PlantillaNoExiste))!;

        public static string TurnoNoExiste =>
            ResourceManager.GetString(nameof(TurnoNoExiste))!;

        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string ResultadoTurnoAsignado =>
            ResourceManager.GetString(nameof(ResultadoTurnoAsignado))!;

        public static string NotaVisibilidadEventual =>
            ResourceManager.GetString(nameof(NotaVisibilidadEventual))!;
    }
}
