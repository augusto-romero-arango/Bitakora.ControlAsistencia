using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.SincronizarLimitesDeJornadaDePlantillaSemanalFunction.CommandHandler;

public partial class SincronizarLimitesDeJornadaDePlantillaSemanalCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.SincronizarLimitesDeJornadaDePlantillaSemanalFunction.CommandHandler.SincronizarLimitesDeJornadaDePlantillaSemanalCommandHandlerMensajes",
        typeof(SincronizarLimitesDeJornadaDePlantillaSemanalCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string PlantillaNoEncontrada =>
            ResourceManager.GetString(nameof(PlantillaNoEncontrada))!;
    }
}
