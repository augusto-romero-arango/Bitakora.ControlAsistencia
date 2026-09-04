using System.Resources;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Issue #335: mensajes propios de FranjaOrdinaria (invariante y label de Sede), en .resx separado
// (MEF-ADR-0009). Los demas mensajes/labels de FranjaOrdinaria (DuracionNoPositiva, Descansos,
// Extras) siguen viviendo en FranjaTemporal.Mensajes (compartido con SubFranja) -- Sede y el tope
// de 24 horas (Issue #598) son exclusivos de FranjaOrdinaria, asi que su Mensajes vive aqui, no alla.
// internal: accesible desde tests via InternalsVisibleTo en el .csproj (mismo patron que
// TurnoCreado.Mensajes).
public sealed partial class FranjaOrdinaria
{
    private static readonly ResourceManager ResourceManager = new(
        "Bitakora.ControlAsistencia.Programacion.DomainEvents.FranjaOrdinariaMensajes",
        typeof(FranjaOrdinaria).Assembly);

    // 'new' explicito: FranjaOrdinaria ya hereda FranjaTemporal.Mensajes (DuracionNoPositiva,
    // Descansos/Extras, contencion/solapamiento) -- este Mensajes propio SOLO cubre lo exclusivo
    // de la sede y el tope de 24 horas, sin duplicar ni reemplazar los mensajes heredados.
    internal new static class Mensajes
    {
        public static string SedeIncompleta =>
            ResourceManager.GetString(nameof(SedeIncompleta))!;

        public static string LabelSede =>
            ResourceManager.GetString(nameof(LabelSede))!;

        public static string DuracionExcedeUnDia =>
            ResourceManager.GetString(nameof(DuracionExcedeUnDia))!;
    }
}
