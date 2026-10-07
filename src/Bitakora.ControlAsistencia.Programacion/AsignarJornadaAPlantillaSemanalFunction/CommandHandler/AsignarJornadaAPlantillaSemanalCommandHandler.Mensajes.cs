using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.AsignarJornadaAPlantillaSemanalFunction.CommandHandler;

public partial class AsignarJornadaAPlantillaSemanalCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.AsignarJornadaAPlantillaSemanalFunction.CommandHandler.AsignarJornadaAPlantillaSemanalCommandHandlerMensajes",
        typeof(AsignarJornadaAPlantillaSemanalCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string PlantillaNoEncontrada =>
            ResourceManager.GetString(nameof(PlantillaNoEncontrada))!;

        public static string JornadaNoEncontrada =>
            ResourceManager.GetString(nameof(JornadaNoEncontrada))!;

        public static string PlantillaRetirada =>
            ResourceManager.GetString(nameof(PlantillaRetirada))!;
    }
}
