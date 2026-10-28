using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.SincronizarLimitesDeJornadaDePlantillaTurnosFunction.CommandHandler;

public partial class SincronizarLimitesDeJornadaDePlantillaTurnosCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.SincronizarLimitesDeJornadaDePlantillaTurnosFunction.CommandHandler.SincronizarLimitesDeJornadaDePlantillaTurnosCommandHandlerMensajes",
        typeof(SincronizarLimitesDeJornadaDePlantillaTurnosCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string PlantillaNoEncontrada =>
            ResourceManager.GetString(nameof(PlantillaNoEncontrada))!;
    }
}
