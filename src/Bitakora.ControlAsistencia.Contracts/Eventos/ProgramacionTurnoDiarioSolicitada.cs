using Bitakora.ControlAsistencia.Contracts.ValueObjects;
using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.Contracts.Eventos;

/// <summary>
/// Evento publico que se publica al Service Bus via IPublicEventSender.
/// Se emite uno por cada fecha del arreglo del comando.
/// Consumidor: ControlHoras.
/// </summary>
public sealed class ProgramacionTurnoDiarioSolicitada : IPublicEvent
{
    public Guid SolicitudId { get; private set; }
    public InformacionEmpleado Empleado { get; private set; } = null!;
    public DateOnly Fecha { get; private set; }
    public DetalleTurno DetalleTurno { get; private set; } = null!;

    public ProgramacionTurnoDiarioSolicitada(
        Guid solicitudId,
        InformacionEmpleado empleado,
        DateOnly fecha,
        DetalleTurno detalleTurno)
    {
        SolicitudId = solicitudId;
        Empleado = empleado;
        Fecha = fecha;
        DetalleTurno = detalleTurno;
    }

    // Constructor para Marten/serializacion
    private ProgramacionTurnoDiarioSolicitada() { }
}
