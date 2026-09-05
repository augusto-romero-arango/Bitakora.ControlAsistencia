namespace Bitakora.ControlAsistencia.Programacion.QuitarSubFranjaFunction;

// Sin TurnoId: viaja en la ruta (programacion/turnos/{id}:quitar-subfranja), no en el body.
// Tipo viaja como string (case-insensitive), mismo criterio de frontera que AgregarSubFranjaBody.
public record QuitarSubFranjaBody(TimeOnly Franja, string Tipo, TimeOnly Inicio);
