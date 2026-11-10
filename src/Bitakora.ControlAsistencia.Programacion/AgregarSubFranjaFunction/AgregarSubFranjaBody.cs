namespace Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction;

// Sin TurnoId: viaja en la ruta. Tipo es string (case-insensitive) porque las opciones Web de STJ no
// aceptan enums como string sin converter; el validator lo valida y el endpoint lo traduce al enum.
public record AgregarSubFranjaBody(TimeOnly Franja, string Tipo, TimeOnly Inicio, TimeOnly Fin);
