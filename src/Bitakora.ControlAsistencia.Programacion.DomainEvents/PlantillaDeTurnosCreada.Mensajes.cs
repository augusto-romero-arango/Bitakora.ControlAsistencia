using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed partial class PlantillaDeTurnosCreada
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.DomainEvents.PlantillaDeTurnosCreadaMensajes",
        typeof(PlantillaDeTurnosCreada).Assembly);

    internal static class Mensajes
    {
        public static string NombreVacio =>
            ResourceManager.GetString(nameof(NombreVacio))!;

        public static string DiasDelCicloFueraDeRango =>
            ResourceManager.GetString(nameof(DiasDelCicloFueraDeRango))!;
    }
}
