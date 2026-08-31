namespace Bitakora.ControlAsistencia.Colaboradores.Entities;

// CA-ADR-0030, dos mecanismos en un mismo resultado: VinculacionTerminada es rechazo (el handler la
// traduce a 409 con mensaje .resx), SinCambios es exito silencioso (202 sin evento nuevo) -- no un
// error.
internal enum ResultadoAsignacionSede
{
    Exitosa,
    SinCambios,
    VinculacionTerminada
}
