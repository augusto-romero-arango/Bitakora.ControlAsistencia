namespace Bitakora.ControlAsistencia.Programacion.Entities;

// Mecanismo "declinar con resultado" (CA-ADR-0030): el aggregate nunca lanza -- retorna la razon
// por la que el turno no es asignable a una nueva solicitud. Precedencia: Retirado antes que
// Incompleto (un turno retirado no se evalua por completitud).
internal enum ResultadoAsignabilidadTurno
{
    Asignable,
    Retirado,
    Incompleto
}
