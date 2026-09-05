using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.AsignarSedeAFranjaFunction;

// Comando interno: el endpoint lo compone desde el {id} de la ruta mas el body. Sede null = retirar.
public record AsignarSedeAFranja(Guid TurnoId, TimeOnly Franja, SedeProgramada? Sede);
