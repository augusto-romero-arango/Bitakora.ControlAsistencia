namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

// Payload propio de esta isla, espejo de PrivateEvents.ControlHoras.MarcacionDelDia
// (payload por rol, MEF-ADR-0039 decision #6).
// Issue #484: la sede MARCADA viaja plana y opcional -- null hasta que llega el estampado.
public record MarcacionDelDia(
    DateTime Timestamp,
    string? Tipo,
    string? CodigoSede = null,
    string? NombreSede = null,
    string? CentroDeCostos = null);
