using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

// Issue #319 (tres islas, MEF-ADR-0039 decision 2): Empleado y DetalleTurno tipan con los records
// propios del dominio (Programacion.DomainEvents) -- ya no con InformacionEmpleado (PublicEvents)
// ni DetalleTurno (PrivateEvents).
public partial class SolicitudProgramacionAggregateRoot : AggregateRoot
{
    internal Empleado? Empleado { get; private set; }
    internal IReadOnlyList<DateOnly> Fechas { get; private set; } = [];
    internal TurnoProgramado? DetalleTurno { get; private set; }

    public void Apply(ProgramacionTurnoSolicitada e)
    {
        Id = e.Id.ToString();
        Empleado = e.Empleado;
        Fechas = e.Fechas;
        DetalleTurno = e.DetalleTurno;
    }

    internal static SolicitudProgramacionAggregateRoot Iniciar(ProgramacionTurnoSolicitada evento)
    {
        var solicitud = new SolicitudProgramacionAggregateRoot();
        solicitud._uncommittedEvents.Add(evento);
        solicitud.Apply(evento);
        return solicitud;
    }
}
