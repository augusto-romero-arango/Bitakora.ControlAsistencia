namespace Bitakora.ControlAsistencia.Programacion.QuitarSubFranjaFunction;

// Sin TurnoId: viaja en la ruta. Tipo es string validado, como en AgregarSubFranjaBody; el endpoint lo
// traduce al enum.
public record QuitarSubFranjaBody(TimeOnly Franja, string Tipo, TimeOnly Inicio);
