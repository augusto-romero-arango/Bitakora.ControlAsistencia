namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

/// <summary>
/// Se persiste en el stream de SolicitudProgramacionAggregateRoot; no se publica al Service Bus.
/// </summary>
/// <remarks>
/// Las claves JSON persistidas no tienen mapeo (ni JsonPropertyName ni upcasters): renombrar una
/// propiedad exige purgar los streams en el mismo despliegue (MEF-ADR-0036 seccion 5). El alias del
/// evento deriva del nombre simple de la clase, que no se renombra.
/// </remarks>
public sealed class ProgramacionTurnoSolicitada
{
    public Guid Id { get; private set; }
    public ColaboradorProgramado Colaborador { get; private set; } = null!;
    public IReadOnlyList<DateOnly> Fechas { get; private set; } = [];
    public TurnoProgramado DetalleTurno { get; private set; } = null!;

    // Sede efectiva del dia (null = sin sede). Los streams anteriores al campo no traen la clave y STJ
    // deja null.
    public SedeProgramada? Sede { get; private set; }

    public JornadaProgramada? Jornada { get; private set; }

    public ProgramacionTurnoSolicitada(
        Guid id,
        ColaboradorProgramado colaborador,
        IReadOnlyList<DateOnly> fechas,
        TurnoProgramado detalleTurno,
        SedeProgramada? sede = null,
        JornadaProgramada? jornada = null)
    {
        Id = id;
        Colaborador = colaborador;
        Fechas = fechas;
        DetalleTurno = detalleTurno;
        Sede = sede;
        Jornada = jornada;
    }

    // Constructor para Marten/serializacion
    private ProgramacionTurnoSolicitada() { }
}
