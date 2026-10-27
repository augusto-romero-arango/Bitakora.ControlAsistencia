using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.AsignarJornadaAPlantillaTurnosFunction.CommandHandler;

public partial class AsignarJornadaAPlantillaTurnosCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.AsignarJornadaAPlantillaTurnosFunction.CommandHandler.AsignarJornadaAPlantillaTurnosCommandHandlerMensajes",
        typeof(AsignarJornadaAPlantillaTurnosCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string PlantillaNoEncontrada =>
            ResourceManager.GetString(nameof(PlantillaNoEncontrada))!;

        public static string JornadaNoEncontrada =>
            ResourceManager.GetString(nameof(JornadaNoEncontrada))!;

        public static string PlantillaRetirada =>
            ResourceManager.GetString(nameof(PlantillaRetirada))!;

        public static string JornadaDesactivada =>
            ResourceManager.GetString(nameof(JornadaDesactivada))!;
    }
}
