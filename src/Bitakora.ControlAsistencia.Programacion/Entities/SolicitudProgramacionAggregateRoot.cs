using Bitakora.ControlAsistencia.Contracts.ValueObjects;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction.Eventos;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

public partial class SolicitudProgramacionAggregateRoot : AggregateRoot
{
    internal InformacionEmpleado? Empleado { get; private set; }
    internal IReadOnlyList<DateOnly> Fechas { get; private set; } = [];
    internal DetalleTurno? DetalleTurno { get; private set; }

    private void Apply(ProgramacionTurnoSolicitada e) => throw new NotImplementedException();

    internal static SolicitudProgramacionAggregateRoot Iniciar(ProgramacionTurnoSolicitada evento)
        => throw new NotImplementedException();
}
