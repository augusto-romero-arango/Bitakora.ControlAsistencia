using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.PrivateEvents.Programacion;

/// <summary>
/// Evento privado intra-BC que se publica al namespace interno del Bounded Context
/// via IPrivateEventSender (ADR-0024 decision #3: todo evento privado cruza fisicamente el ASB).
/// Se emite uno por cada fecha del arreglo del comando.
/// Consumidor: ControlHoras (mismo Bounded Context "Control de Asistencia") -> es intra-BC,
/// por eso es IPrivateEvent y no IPublicEvent (ADR-0024 decision #2).
/// Todo su payload es propio de este ensamblado -- DetalleEmpleado incluido, que duplica a
/// InformacionEmpleado (PublicEvents) en vez de importarlo (CA-ADR-0029 decisiones #2 y #5).
/// </summary>
public sealed class ProgramacionTurnoDiarioSolicitada : IPrivateEvent
{
    public Guid SolicitudId { get; private set; }
    public DetalleEmpleado Empleado { get; private set; } = null!;
    public DateOnly Fecha { get; private set; }
    public DetalleTurno DetalleTurno { get; private set; } = null!;

    public ProgramacionTurnoDiarioSolicitada(
        Guid solicitudId,
        DetalleEmpleado empleado,
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
