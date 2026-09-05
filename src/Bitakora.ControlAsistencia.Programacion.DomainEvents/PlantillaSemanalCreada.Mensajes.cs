using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// MEF-ADR-0009: mensajes del evento PlantillaSemanalCreada en archivo .resx separado.
// internal: accesible desde tests via InternalsVisibleTo en el .csproj.
public sealed partial class PlantillaSemanalCreada
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.DomainEvents.PlantillaSemanalCreadaMensajes",
        typeof(PlantillaSemanalCreada).Assembly);

    internal static class Mensajes
    {
        public static string NombreVacio =>
            ResourceManager.GetString(nameof(NombreVacio))!;

        public static string SemanasFueraDeRango =>
            ResourceManager.GetString(nameof(SemanasFueraDeRango))!;
    }
}
