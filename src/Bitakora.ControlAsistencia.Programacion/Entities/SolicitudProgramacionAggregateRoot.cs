using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

// Issue #319 (tres islas, MEF-ADR-0039 decision 2): Colaborador y DetalleTurno tipan con los records
// propios del dominio (Programacion.DomainEvents) -- ya no con InformacionColaborador
// (PublicEvents) ni DetalleTurno (PrivateEvents). Issue #340: el record del colaborador se llama
// ColaboradorProgramado. Issue #401: la propiedad paso de Empleado a Colaborador, alineada con la
// clave JSON del evento persistido que la hidrata.
public partial class SolicitudProgramacionAggregateRoot : AggregateRoot
{
    internal ColaboradorProgramado? Colaborador { get; private set; }
    internal IReadOnlyList<DateOnly> Fechas { get; private set; } = [];
    internal TurnoProgramado? DetalleTurno { get; private set; }

    // Issue #331: sede efectiva del dia, opcional (null = sin sede asignada).
    internal SedeProgramada? Sede { get; private set; }

    public void Apply(ProgramacionTurnoSolicitada e)
    {
        Id = e.Id.ToString();
        Colaborador = e.Colaborador;
        Fechas = e.Fechas;
        DetalleTurno = e.DetalleTurno;
        Sede = e.Sede;
    }

    internal static SolicitudProgramacionAggregateRoot Iniciar(ProgramacionTurnoSolicitada evento)
    {
        var solicitud = new SolicitudProgramacionAggregateRoot();
        solicitud._uncommittedEvents.Add(evento);
        solicitud.Apply(evento);
        return solicitud;
    }
}
