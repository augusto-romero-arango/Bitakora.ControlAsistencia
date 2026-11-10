using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.PrivateEvents.Programacion;

/// <summary>Sin detalle de turno: cancelar no transporta plan.</summary>
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
