using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.CrearPlantillaSemanalFunction.CommandHandler;

// Mensajes internal, no private: los tests los leen via InternalsVisibleTo del .csproj.
public partial class CrearPlantillaSemanalCommandHandler
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.CrearPlantillaSemanalFunction.CommandHandler.CrearPlantillaSemanalCommandHandlerMensajes",
        typeof(CrearPlantillaSemanalCommandHandler).Assembly);

    internal static class Mensajes
    {
        public static string PlantillaYaExiste =>
            ResourceManager.GetString(nameof(PlantillaYaExiste))!;
    }
}
