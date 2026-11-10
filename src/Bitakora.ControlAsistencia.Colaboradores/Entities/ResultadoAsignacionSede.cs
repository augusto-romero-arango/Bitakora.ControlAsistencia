namespace Bitakora.ControlAsistencia.Colaboradores.Entities;

// VinculacionTerminada es rechazo (el handler la traduce a 409 con mensaje .resx, CA-ADR-0030);
// SinCambios es exito sin evento nuevo (204, CA-ADR-0035), no un error.
internal enum ResultadoAsignacionSede
{
    Exitosa,
    SinCambios,
    VinculacionTerminada
}
