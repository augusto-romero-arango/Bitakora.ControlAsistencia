namespace Bitakora.ControlAsistencia.Sedes.DomainEvents;

// Ciudad/Direccion son informativas, sin regla de negocio propia.
public record SedeRegistrada(string Codigo, string Nombre, string? Ciudad, string? Direccion);
