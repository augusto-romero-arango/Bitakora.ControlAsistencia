namespace Bitakora.ControlAsistencia.Programacion.QuitarFranjaFunction;

// Comando interno: el endpoint lo compone desde el {id} de la ruta mas el body.
public record QuitarFranja(Guid TurnoId, TimeOnly Franja);
