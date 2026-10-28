using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed partial class DiaDePlantillaDeTurnosAsignado
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.DomainEvents.DiaDePlantillaDeTurnosAsignadoMensajes",
        typeof(DiaDePlantillaDeTurnosAsignado).Assembly);

    internal static class Mensajes
    {
        public static string DiaNoPositivo =>
            ResourceManager.GetString(nameof(DiaNoPositivo))!;
    }
}
