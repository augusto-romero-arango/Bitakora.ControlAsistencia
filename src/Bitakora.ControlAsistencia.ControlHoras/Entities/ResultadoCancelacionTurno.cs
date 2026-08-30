namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// Resultado de ControlDiarioAggregateRoot.CancelarTurno. Mismo mecanismo "declinar con resultado"
// que ResultadoEstampadoSede (CA-ADR-0030): el handler no interroga DetalleTurno antes de decidir
// (Tell-don't-Ask, MEF-ADR-0012).
//   - Cancelado: habia un turno asignado -- se persiste TurnoDiarioCancelado y se republica DiaDepurado.
//   - SinTurnoAsignado: no-op silencioso (issue #499) -- el stream no tenia turno que cancelar (ya
//     cancelado, o el dia nacio solo por marcaciones). Sin evento nuevo ni republicacion.
internal enum ResultadoCancelacionTurno
{
    Cancelado,
    SinTurnoAsignado
}
