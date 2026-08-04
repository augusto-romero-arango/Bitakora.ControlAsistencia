using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.PublicEvents.Empleados;

namespace Bitakora.ControlAsistencia.ControlHoras.ObtenerTurnoDiario;

/// <summary>
/// DTO de respuesta de ObtenerTurnoDiario (issue #289, CA-5). NO es el read model: omite el
/// <c>Id</c> de <see cref="ReadModels.ControlHoras.TurnoDiarioView"/> -- ese id es el stream key
/// que compone <c>ControlDiarioAggregateRoot.ComputarStreamId</c>, una decision interna del event
/// store que no aporta nada que no este ya en <see cref="Empleado"/> y <see cref="Fecha"/>.
///
/// Vive en el Function App (ControlHoras), no en ReadModels: es el contrato HTTP, no la vista
/// materializada.
/// </summary>
public sealed record TurnoDiarioRespuesta(
    InformacionEmpleado Empleado,
    DateOnly Fecha,
    DetalleTurno DetalleTurno,
    Guid UltimaSolicitudId);
