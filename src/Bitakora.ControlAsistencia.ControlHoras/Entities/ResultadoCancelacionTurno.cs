namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// Resultado de ControlDiarioAggregateRoot.CancelarTurno. Mismo mecanismo "declinar con resultado"
// que ResultadoEstampadoSede (CA-ADR-0030):
//   - Cancelado: habia un turno asignado -- se persiste TurnoDiarioCancelado y se republica DiaDepurado.
//   - SinTurnoAsignado: no-op silencioso -- el stream no tenia turno que cancelar (ya cancelado, o el
//     dia nacio solo por marcaciones). Sin evento nuevo ni republicacion.
// internal: mismo criterio de visibilidad que los resultados hermanos -- vive en el mismo ensamblado
// que el handler que lo consume.
internal enum ResultadoCancelacionTurno
{
    Cancelado,
    SinTurnoAsignado
}
