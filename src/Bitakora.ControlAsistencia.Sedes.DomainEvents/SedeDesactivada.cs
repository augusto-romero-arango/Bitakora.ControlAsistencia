namespace Bitakora.ControlAsistencia.Sedes.DomainEvents;

// Issue #459: la sede nace activa (sin evento inicial de activacion) -- este es el primer evento
// que existe para la bandera Activa. Sin payload -- el hecho es el evento mismo.
public record SedeDesactivada;
