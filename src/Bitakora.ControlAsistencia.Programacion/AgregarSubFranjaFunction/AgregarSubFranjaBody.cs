namespace Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction;

// Sin TurnoId: viaja en la ruta (programacion/turnos/{id}:agregar-subfranja), no en el body.
// Tipo viaja como string (case-insensitive): no hay precedente de enums en DTOs HTTP de este
// repo y las opciones Web de STJ no aceptan enums como string sin converter -- el string validado
// por AgregarSubFranjaBodyValidator evita ese gate; el endpoint lo traduce al enum interno.
public record AgregarSubFranjaBody(TimeOnly Franja, string Tipo, TimeOnly Inicio, TimeOnly Fin);
