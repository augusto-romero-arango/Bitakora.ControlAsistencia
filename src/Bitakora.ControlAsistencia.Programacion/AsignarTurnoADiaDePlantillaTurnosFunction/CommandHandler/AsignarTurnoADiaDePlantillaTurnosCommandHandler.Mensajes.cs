using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.AsignarTurnoADiaDePlantillaTurnosFunction.CommandHandler;

public partial class AsignarTurnoADiaDePlantillaTurnosCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.AsignarTurnoADiaDePlantillaTurnosFunction.CommandHandler.AsignarTurnoADiaDePlantillaTurnosCommandHandlerMensajes",
        typeof(AsignarTurnoADiaDePlantillaTurnosCommandHandler).Assembly);

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

        public static string DiaFueraDelCiclo =>
            ResourceManager.GetString(nameof(DiaFueraDelCiclo))!;
    }
}
