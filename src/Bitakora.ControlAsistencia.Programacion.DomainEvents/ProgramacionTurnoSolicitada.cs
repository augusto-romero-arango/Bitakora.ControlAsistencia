namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

/// <summary>
/// Evento de event sourcing (privado). Se persiste en el stream de SolicitudProgramacionAggregateRoot.
/// No se publica al Service Bus.
/// </summary>
/// <remarks>
/// Issue #319 (tres islas, MEF-ADR-0039 decision 2 y 6): Colaborador y DetalleTurno tipan con los
/// records propios de este ensamblado (ColaboradorProgramado, TurnoProgramado) en vez de
/// InformacionColaborador (PublicEvents) y DetalleTurno (PrivateEvents). Issue #340 renombro el
/// TIPO del payload (de Empleado a ColaboradorProgramado) conservando las claves JSON.
///
/// Issue #401: la propiedad paso de Empleado a Colaborador -- este si cambia la clave JSON
/// persistida en mt_events, a diferencia de #319/#322/#340. Sin mapeo (nada de JsonPropertyName ni
/// upcasters): los streams de dev se purgan en el mismo despliegue que integra el cambio
/// (MEF-ADR-0036 seccion 5) y los smoke tests los repueblan con el vocabulario nuevo. El alias del
/// evento NO se toca: deriva del nombre simple de la clase, que no se renombra.
/// </remarks>
public sealed class ProgramacionTurnoSolicitada
{
    public Guid Id { get; private set; }
    public ColaboradorProgramado Colaborador { get; private set; } = null!;
    public IReadOnlyList<DateOnly> Fechas { get; private set; } = [];
    public TurnoProgramado DetalleTurno { get; private set; } = null!;

    // Issue #331: sede efectiva del dia, opcional (null = sin sede asignada). Campo aditivo: los
    // streams escritos antes de este issue no llevan la clave "Sede" en el JSON persistido; STJ
    // deja null en el parametro posicional opcional (ver ProgramacionTurnoSolicitadaSerializacionTests,
    // precedente #288/#319).
    public SedeProgramada? Sede { get; private set; }

    public ProgramacionTurnoSolicitada(
        Guid id,
        ColaboradorProgramado colaborador,
        IReadOnlyList<DateOnly> fechas,
        TurnoProgramado detalleTurno,
        SedeProgramada? sede = null)
    {
        Id = id;
        Colaborador = colaborador;
        Fechas = fechas;
        DetalleTurno = detalleTurno;
        Sede = sede;
    }

    // Constructor para Marten/serializacion
    private ProgramacionTurnoSolicitada() { }
}
