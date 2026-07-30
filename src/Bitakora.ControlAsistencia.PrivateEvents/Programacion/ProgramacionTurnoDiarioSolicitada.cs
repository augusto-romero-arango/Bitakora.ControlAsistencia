using Bitakora.ControlAsistencia.PublicEvents.Empleados;
using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.PrivateEvents.Programacion;

/// <summary>
/// Evento privado intra-BC que se publica al namespace interno del Bounded Context
/// via IPrivateEventSender (ADR-0024 decision #3: todo evento privado cruza fisicamente el ASB).
/// Se emite uno por cada fecha del arreglo del comando.
/// Consumidor: ControlHoras (mismo Bounded Context "Control de Asistencia") -> es intra-BC,
/// por eso es IPrivateEvent y no IPublicEvent (ADR-0024 decision #2).
/// </summary>
public sealed class ProgramacionTurnoDiarioSolicitada : IPrivateEvent
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
