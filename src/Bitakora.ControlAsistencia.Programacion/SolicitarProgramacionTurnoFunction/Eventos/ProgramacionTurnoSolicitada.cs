using Bitakora.ControlAsistencia.Contracts.Empleados.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;

namespace Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction.Eventos;

/// <summary>
/// Evento de event sourcing (privado). Se persiste en el stream de SolicitudProgramacionAggregateRoot.
/// No se publica al Service Bus.
/// </summary>
public sealed class ProgramacionTurnoSolicitada
{
    public Guid Id { get; private set; }
    public InformacionEmpleado Empleado { get; private set; } = null!;
    public IReadOnlyList<DateOnly> Fechas { get; private set; } = [];
    public DetalleTurno DetalleTurno { get; private set; } = null!;

    public ProgramacionTurnoSolicitada(
        Guid id,
        InformacionEmpleado empleado,
        IReadOnlyList<DateOnly> fechas,
        DetalleTurno detalleTurno)
    {
        Id = id;
        Empleado = empleado;
        Fechas = fechas;
        DetalleTurno = detalleTurno;
    }

    // Constructor para Marten/serializacion
    private ProgramacionTurnoSolicitada() { }
}
