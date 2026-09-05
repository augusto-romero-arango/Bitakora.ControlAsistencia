namespace Bitakora.ControlAsistencia.Programacion.Entities;

// SinCambios es exito silencioso (204 sin evento nuevo, el dia ya estaba vacio), no un rechazo --
// mismo mecanismo que ResultadoAsignarDia.
internal enum ResultadoQuitarDia
{
    Quitado,
    SinCambios,
    SemanaFueraDeRango
}
