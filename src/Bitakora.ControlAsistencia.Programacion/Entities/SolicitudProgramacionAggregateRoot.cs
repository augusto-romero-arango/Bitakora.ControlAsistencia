using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

public partial class SolicitudProgramacionAggregateRoot : AggregateRoot
{
    internal ColaboradorProgramado? Colaborador { get; private set; }
    internal IReadOnlyList<DateOnly> Fechas { get; private set; } = [];
    internal TurnoProgramado? DetalleTurno { get; private set; }

    // Sede efectiva del dia (null = sin sede asignada).
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
