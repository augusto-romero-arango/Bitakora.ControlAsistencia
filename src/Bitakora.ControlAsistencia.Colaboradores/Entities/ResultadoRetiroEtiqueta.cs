namespace Bitakora.ControlAsistencia.Colaboradores.Entities;

// SinCambios es EXITO sin evento (la categoria no estaba), no un rechazo (MEF-ADR-0004 "Estado ya
// alcanzado").
internal enum ResultadoRetiroEtiqueta
{
    Exitosa,
    SinCambios,
    VinculacionTerminada
}
