using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed partial class DiaDePlantillaSemanalQuitado
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.DomainEvents.DiaDePlantillaSemanalQuitadoMensajes",
        typeof(DiaDePlantillaSemanalQuitado).Assembly);

    // internal, no private: los tests los leen via InternalsVisibleTo del .csproj.
    internal static class Mensajes
    {
        public static string SemanaNoPositiva =>
            ResourceManager.GetString(nameof(SemanaNoPositiva))!;
    }
}
