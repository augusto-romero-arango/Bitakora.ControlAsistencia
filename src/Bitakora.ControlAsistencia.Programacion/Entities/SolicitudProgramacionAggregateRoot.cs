using Bitakora.ControlAsistencia.Contracts.ValueObjects;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction.Eventos;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

public partial class SolicitudProgramacionAggregateRoot : AggregateRoot
{
    internal InformacionEmpleado? Empleado { get; private set; }
    internal IReadOnlyList<DateOnly> Fechas { get; private set; } = [];
    internal DetalleTurno? DetalleTurno { get; private set; }

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
