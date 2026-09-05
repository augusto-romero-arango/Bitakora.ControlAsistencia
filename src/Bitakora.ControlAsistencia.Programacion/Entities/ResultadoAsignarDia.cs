namespace Bitakora.ControlAsistencia.Programacion.Entities;

// Issue #621: CA-ADR-0030 -- el aggregate declina con resultado, nunca lanza. SinCambios es exito
// silencioso (204 sin evento nuevo, mismo turno ya asignado a ese dia), no un rechazo -- mismo
// mecanismo que ResultadoAsignacionSede (Colaboradores).
internal enum ResultadoAsignarDia
{
    Asignado,
    SinCambios,
    SemanaFueraDeRango
}
