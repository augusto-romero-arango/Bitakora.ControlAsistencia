using System.Resources;

namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// Issue #275, MEF-ADR-0009: mensajes de error de MarcacionRegistrada en archivo .resx separado,
// patron TurnoCreado.Mensajes (Programacion.DomainEvents).
// internal: accesible desde tests via InternalsVisibleTo en el .csproj.
public sealed partial class MarcacionRegistrada
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.ControlHoras.DomainEvents.MarcacionRegistradaMensajes",
        typeof(MarcacionRegistrada).Assembly);

    internal static class Mensajes
    {
        public static string EmpleadoIdVacio =>
            ResourceManager.GetString(nameof(EmpleadoIdVacio))!;
    }
}
