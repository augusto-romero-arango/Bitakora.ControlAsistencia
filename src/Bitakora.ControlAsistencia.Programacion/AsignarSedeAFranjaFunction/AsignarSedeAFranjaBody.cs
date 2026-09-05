using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.AsignarSedeAFranjaFunction;

// Sin TurnoId: viaja en la ruta (programacion/turnos/{id}:asignar-sede-franja), no en el body.
// Sin IValidator<T>: la completitud de Sede la decide FranjaOrdinaria.Crear via ConSede (issue
// #606) -- ausente o null retira la sede prearmada.
public record AsignarSedeAFranjaBody(TimeOnly Franja, SedeProgramada? Sede = null);
