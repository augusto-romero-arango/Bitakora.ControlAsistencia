namespace Bitakora.ControlAsistencia.Programacion.QuitarFranjaFunction;

// Sin TurnoId: viaja en la ruta (programacion/turnos/{id}:quitar-franja), no en el body.
// Sin IValidator<T>: TimeOnly no admite un valor invalido que custodiar.
public record QuitarFranjaBody(TimeOnly Franja);
