namespace Bitakora.ControlAsistencia.Programacion.QuitarSubFranjaFunction;

// Sin TurnoId: viaja en la ruta (programacion/turnos/{id}:quitar-subfranja), no en el body.
// Tipo viaja como string validado, igual que AgregarSubFranjaBody: el endpoint lo traduce al enum.
public record QuitarSubFranjaBody(TimeOnly Franja, string Tipo, TimeOnly Inicio);
