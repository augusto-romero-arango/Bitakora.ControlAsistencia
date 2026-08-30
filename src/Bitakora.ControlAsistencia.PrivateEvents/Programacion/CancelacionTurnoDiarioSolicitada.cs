using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.PrivateEvents.Programacion;

/// <summary>
/// Su unico consumidor, ControlHoras, vive en este mismo Bounded Context: es IPrivateEvent y no
/// IPublicEvent (mismo criterio que su gemela ProgramacionTurnoDiarioSolicitada). Payload propio de
/// PrivateEvents -- sin detalle de turno: cancelar no necesita transportar plan.
/// </summary>
public sealed class CancelacionTurnoDiarioSolicitada : IPrivateEvent
{
    public Guid SolicitudId { get; private set; }
    public ResumenColaborador Colaborador { get; private set; } = null!;
    public DateOnly Fecha { get; private set; }

    public CancelacionTurnoDiarioSolicitada(
        Guid solicitudId,
        ResumenColaborador colaborador,
        DateOnly fecha)
    {
        SolicitudId = solicitudId;
        Colaborador = colaborador;
        Fecha = fecha;
    }

    // Constructor para Marten/serializacion
    private CancelacionTurnoDiarioSolicitada() { }
}
