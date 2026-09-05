using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed partial class PlantillaSemanalCreada
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.DomainEvents.PlantillaSemanalCreadaMensajes",
        typeof(PlantillaSemanalCreada).Assembly);

    // internal, no private: los tests los leen via InternalsVisibleTo del .csproj.
    internal static class Mensajes
    {
        public static string NombreVacio =>
            ResourceManager.GetString(nameof(NombreVacio))!;

        public static string SemanasFueraDeRango =>
            ResourceManager.GetString(nameof(SemanasFueraDeRango))!;
    }
}
