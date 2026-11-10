using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.AsignarSedeAFranjaFunction;

// Sede null = retirar.
public record AsignarSedeAFranja(Guid TurnoId, TimeOnly Franja, SedeProgramada? Sede);
