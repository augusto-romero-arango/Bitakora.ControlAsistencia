namespace Bitakora.ControlAsistencia.Programacion.QuitarFranjaFunction;

// Sin TurnoId: viaja en la ruta. Sin IValidator<T>: TimeOnly no admite un valor invalido que custodiar.
public record QuitarFranjaBody(TimeOnly Franja);
