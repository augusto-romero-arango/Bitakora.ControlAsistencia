using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.QuitarJornadaDePlantillaTurnosFunction.CommandHandler;

public partial class QuitarJornadaDePlantillaTurnosCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.QuitarJornadaDePlantillaTurnosFunction.CommandHandler.QuitarJornadaDePlantillaTurnosCommandHandlerMensajes",
        typeof(QuitarJornadaDePlantillaTurnosCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string PlantillaNoEncontrada =>
            ResourceManager.GetString(nameof(PlantillaNoEncontrada))!;

        public static string PlantillaRetirada =>
            ResourceManager.GetString(nameof(PlantillaRetirada))!;
    }
}
