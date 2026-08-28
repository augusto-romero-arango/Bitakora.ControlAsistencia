namespace Bitakora.ControlAsistencia.Sedes.DomainEvents;

// Issue #456: primer evento persistido de SedeAggregateRoot -- nace el stream de una sede.
// Payload plano (sin VOs anidados): Ciudad/Direccion son informativas, sin regla de negocio propia
// en este issue (decision de sesion 2026-08-27).
public record SedeRegistrada(string Codigo, string Nombre, string? Ciudad, string? Direccion);
