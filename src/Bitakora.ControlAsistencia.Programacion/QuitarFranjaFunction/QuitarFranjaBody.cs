namespace Bitakora.ControlAsistencia.Programacion.QuitarFranjaFunction;

// Sin TurnoId: viaja en la ruta (programacion/turnos/{id}:quitar-franja), no en el body.
// Sin validator: TimeOnly siempre es valido -- no hay invariante que un IValidator<T> deba
// custodiar.
public record QuitarFranjaBody(TimeOnly Franja);
