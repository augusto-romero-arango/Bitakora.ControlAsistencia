using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.AsignarTurnoADiaDePlantillaSemanalFunction.CommandHandler;

public partial class AsignarTurnoADiaDePlantillaSemanalCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.AsignarTurnoADiaDePlantillaSemanalFunction.CommandHandler.AsignarTurnoADiaDePlantillaSemanalCommandHandlerMensajes",
        typeof(AsignarTurnoADiaDePlantillaSemanalCommandHandler).Assembly);

    // internal, no private: los tests los leen via InternalsVisibleTo del .csproj.
    internal static class Mensajes
    {
        public static string PlantillaNoEncontrada =>
            ResourceManager.GetString(nameof(PlantillaNoEncontrada))!;

        public static string TurnoNoEncontrado =>
            ResourceManager.GetString(nameof(TurnoNoEncontrado))!;

        public static string TurnoRetirado =>
            ResourceManager.GetString(nameof(TurnoRetirado))!;

        public static string TurnoIncompleto =>
            ResourceManager.GetString(nameof(TurnoIncompleto))!;

        public static string SemanaFueraDeRango =>
            ResourceManager.GetString(nameof(SemanaFueraDeRango))!;
    }
}
