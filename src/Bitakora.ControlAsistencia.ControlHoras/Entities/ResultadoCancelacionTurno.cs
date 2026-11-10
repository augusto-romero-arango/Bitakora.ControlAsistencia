namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// - Cancelado: se persiste TurnoDiarioCancelado y se republica DiaDepurado.
// - SinTurnoAsignado: no-op silencioso (ya cancelado, o el dia nacio solo por marcaciones), sin
//   evento nuevo ni republicacion.
internal enum ResultadoCancelacionTurno
{
    Cancelado,
    SinTurnoAsignado
}
