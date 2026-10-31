using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.PrivateEvents.Programacion;

public sealed class ProgramacionTurnoEnFechasSolicitada : IPrivateEvent
{
    public Guid SolicitudId { get; private set; }
    public ResumenColaborador Colaborador { get; private set; } = null!;
    public IReadOnlyList<DateOnly> Fechas { get; private set; } = null!;
    public DetalleTurno DetalleTurno { get; private set; } = null!;
    public DetalleJornada Jornada { get; private set; } = null!;
    public DetalleSede? Sede { get; private set; }

    public ProgramacionTurnoEnFechasSolicitada(
        Guid solicitudId,
        ResumenColaborador colaborador,
        IReadOnlyList<DateOnly> fechas,
        DetalleTurno detalleTurno,
        DetalleJornada jornada,
        DetalleSede? sede = null)
    {
        SolicitudId = solicitudId;
        Colaborador = colaborador;
        Fechas = fechas;
        DetalleTurno = detalleTurno;
        Jornada = jornada;
        Sede = sede;
    }

    private ProgramacionTurnoEnFechasSolicitada() { }
}
