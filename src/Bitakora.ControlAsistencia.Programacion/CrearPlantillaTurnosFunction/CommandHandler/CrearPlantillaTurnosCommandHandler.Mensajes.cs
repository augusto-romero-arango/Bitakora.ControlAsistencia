using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.CrearPlantillaTurnosFunction.CommandHandler;

public partial class CrearPlantillaTurnosCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.CrearPlantillaTurnosFunction.CommandHandler.CrearPlantillaTurnosCommandHandlerMensajes",
        typeof(CrearPlantillaTurnosCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string PlantillaYaExiste =>
            ResourceManager.GetString(nameof(PlantillaYaExiste))!;

        public static string JornadaNoEncontrada =>
            ResourceManager.GetString(nameof(JornadaNoEncontrada))!;

        public static string JornadaDesactivada =>
            ResourceManager.GetString(nameof(JornadaDesactivada))!;
    }
}
