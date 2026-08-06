using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.PrivateEvents.Programacion;

/// <summary>
/// Evento privado intra-BC que se publica al namespace interno del Bounded Context
/// via IPrivateEventSender (ADR-0024 decision #3: todo evento privado cruza fisicamente el ASB).
/// Se emite uno por cada fecha del arreglo del comando.
/// Consumidor: ControlHoras (mismo Bounded Context "Control de Asistencia") -> es intra-BC,
/// por eso es IPrivateEvent y no IPublicEvent (ADR-0024 decision #2).
/// Issue #318: Empleado tipa con DetalleEmpleado (payload propio de PrivateEvents, MEF-ADR-0039
/// decision 6) en vez de InformacionEmpleado (PublicEvents) -- PrivateEvents queda sin
/// ProjectReference a PublicEvents (tres islas, CA-ADR-0029 enmendado por #317).
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
