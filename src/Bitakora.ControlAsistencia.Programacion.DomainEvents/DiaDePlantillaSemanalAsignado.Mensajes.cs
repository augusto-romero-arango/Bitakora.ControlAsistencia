using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed partial class DiaDePlantillaSemanalAsignado
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.DomainEvents.DiaDePlantillaSemanalAsignadoMensajes",
        typeof(DiaDePlantillaSemanalAsignado).Assembly);

    // internal, no private: los tests los leen via InternalsVisibleTo del .csproj.
    internal static class Mensajes
    {
        public static string SemanaNoPositiva =>
            ResourceManager.GetString(nameof(SemanaNoPositiva))!;
    }
}
