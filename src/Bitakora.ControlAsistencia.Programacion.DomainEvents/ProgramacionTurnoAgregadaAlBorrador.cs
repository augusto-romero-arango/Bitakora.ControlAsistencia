namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class ProgramacionTurnoAgregadaAlBorrador
{
    public Guid SolicitudId { get; private set; }
    public ColaboradorProgramado Colaborador { get; private set; } = null!;
    public IReadOnlyList<DateOnly> Fechas { get; private set; } = [];
    public TurnoProgramado DetalleTurno { get; private set; } = null!;
    public SedeProgramada? Sede { get; private set; }
    public JornadaProgramada Jornada { get; private set; } = null!;

    public ProgramacionTurnoAgregadaAlBorrador(
        Guid solicitudId,
        ColaboradorProgramado colaborador,
        IReadOnlyList<DateOnly> fechas,
        TurnoProgramado detalleTurno,
        SedeProgramada? sede,
        JornadaProgramada jornada)
    {
        SolicitudId = solicitudId;
        Colaborador = colaborador;
        Fechas = fechas;
        DetalleTurno = detalleTurno;
        Sede = sede;
        Jornada = jornada;
    }

    private ProgramacionTurnoAgregadaAlBorrador() { }
}
