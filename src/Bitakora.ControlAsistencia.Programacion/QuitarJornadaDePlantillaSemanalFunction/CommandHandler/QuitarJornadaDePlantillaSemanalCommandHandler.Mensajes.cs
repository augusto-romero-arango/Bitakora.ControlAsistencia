using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.QuitarJornadaDePlantillaSemanalFunction.CommandHandler;

public partial class QuitarJornadaDePlantillaSemanalCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.QuitarJornadaDePlantillaSemanalFunction.CommandHandler.QuitarJornadaDePlantillaSemanalCommandHandlerMensajes",
        typeof(QuitarJornadaDePlantillaSemanalCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string PlantillaNoEncontrada =>
            ResourceManager.GetString(nameof(PlantillaNoEncontrada))!;

        public static string PlantillaRetirada =>
            ResourceManager.GetString(nameof(PlantillaRetirada))!;
    }
}
