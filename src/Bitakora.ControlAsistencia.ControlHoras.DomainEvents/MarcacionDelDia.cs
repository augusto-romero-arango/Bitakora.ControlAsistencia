namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// Payload propio de esta isla, espejo de PrivateEvents.ControlHoras.MarcacionDelDia
// (payload por rol, MEF-ADR-0039 decision #6).
public record MarcacionDelDia(DateTime Timestamp, string? Tipo);
