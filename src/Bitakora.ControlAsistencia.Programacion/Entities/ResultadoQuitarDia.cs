namespace Bitakora.ControlAsistencia.Programacion.Entities;

// Issue #622: CA-ADR-0030 -- el aggregate declina con resultado, nunca lanza. SinCambios es exito
// silencioso (204 sin evento nuevo, el dia ya estaba vacio), no un rechazo -- mismo mecanismo que
// ResultadoAsignarDia. Precedencia: semana fuera de rango > sin cambios (idempotencia) > quitado.
internal enum ResultadoQuitarDia
{
    Quitado,
    SinCambios,
    SemanaFueraDeRango
}
