using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.AsignarSedeAFranjaFunction;

// Sin TurnoId: viaja en la ruta. Sin IValidator<T>: la completitud de Sede la decide
// FranjaOrdinaria.Crear via ConSede; ausente o null retira la sede prearmada.
public record AsignarSedeAFranjaBody(TimeOnly Franja, SedeProgramada? Sede = null);
