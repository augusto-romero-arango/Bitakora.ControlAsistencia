using System.Resources;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.CrearPlantillaRotativa;

public partial class CrearPlantillaRotativaTool
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Mcp.Asistente.CrearPlantillaRotativa.CrearPlantillaRotativaToolMensajes",
        typeof(CrearPlantillaRotativaTool).Assembly);

    internal static class Mensajes
    {
        public static string CampoObligatorio =>
            ResourceManager.GetString(nameof(CampoObligatorio))!;

        public static string BloquesJsonInvalido =>
            ResourceManager.GetString(nameof(BloquesJsonInvalido))!;

        public static string BloquesVacio =>
            ResourceManager.GetString(nameof(BloquesVacio))!;

        public static string BloqueDiasInvalido =>
            ResourceManager.GetString(nameof(BloqueDiasInvalido))!;

        public static string SumaDeDiasFueraDeRango =>
            ResourceManager.GetString(nameof(SumaDeDiasFueraDeRango))!;

        public static string TurnoPorNombreNoSoportado =>
            ResourceManager.GetString(nameof(TurnoPorNombreNoSoportado))!;

        public static string TurnoOFranjaObligatorio =>
            ResourceManager.GetString(nameof(TurnoOFranjaObligatorio))!;

        public static string TurnoYFranjaExcluyentes =>
            ResourceManager.GetString(nameof(TurnoYFranjaExcluyentes))!;

        public static string TurnoIdInvalido =>
            ResourceManager.GetString(nameof(TurnoIdInvalido))!;

        public static string FranjaInvalida =>
            ResourceManager.GetString(nameof(FranjaInvalida))!;

        public static string TurnosNoExisten =>
            ResourceManager.GetString(nameof(TurnosNoExisten))!;

        public static string JornadaIdInvalido =>
            ResourceManager.GetString(nameof(JornadaIdInvalido))!;

        public static string JornadaNoExiste =>
            ResourceManager.GetString(nameof(JornadaNoExiste))!;

        public static string RechazoDelDominio =>
            ResourceManager.GetString(nameof(RechazoDelDominio))!;

        public static string ResultadoPlantillaCreada =>
            ResourceManager.GetString(nameof(ResultadoPlantillaCreada))!;

        public static string NotaVisibilidadEventual =>
            ResourceManager.GetString(nameof(NotaVisibilidadEventual))!;

        public static string TurnoInlineEnConflicto =>
            ResourceManager.GetString(nameof(TurnoInlineEnConflicto))!;

        public static string TurnoInlineNombreDuplicado =>
            ResourceManager.GetString(nameof(TurnoInlineNombreDuplicado))!;

        public static string FalloTurnoInline =>
            ResourceManager.GetString(nameof(FalloTurnoInline))!;

        public static string NingunTurnoInlineCreado =>
            ResourceManager.GetString(nameof(NingunTurnoInlineCreado))!;
    }
}
