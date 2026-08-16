namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

/// <summary>
/// Evento de event sourcing (privado). Se persiste en el stream de SolicitudProgramacionAggregateRoot.
/// No se publica al Service Bus.
/// </summary>
/// <remarks>
/// Issue #319 (tres islas, MEF-ADR-0039 decision 2 y 6): Empleado y DetalleTurno tipan con los
/// records propios de este ensamblado (ColaboradorProgramado, TurnoProgramado) en vez de
/// InformacionColaborador (PublicEvents) y DetalleTurno (PrivateEvents). Issue #340: el record
/// paso de llamarse Empleado a ColaboradorProgramado -- solo el TIPO; el nombre de la propiedad
/// (la clave JSON "Empleado") se conserva hasta #401. Los NOMBRES de las propiedades no cambian --
/// son las claves JSON persistidas en mt_events (CA-2); solo cambian los TIPOS. Sin migracion de
/// datos: STJ no persiste $type para records anidados, asi que el JSON ya escrito deserializa
/// identico contra los tipos nuevos (ver ProgramacionTurnoSolicitadaSerializacionTests).
/// </remarks>
public sealed class ProgramacionTurnoSolicitada
{
    public Guid Id { get; private set; }
    public ColaboradorProgramado Empleado { get; private set; } = null!;
    public IReadOnlyList<DateOnly> Fechas { get; private set; } = [];
    public TurnoProgramado DetalleTurno { get; private set; } = null!;

    // Issue #331: sede efectiva del dia, opcional (null = sin sede asignada). Campo aditivo: los
    // streams escritos antes de este issue no llevan la clave "Sede" en el JSON persistido; STJ
    // deja null en el parametro posicional opcional (ver ProgramacionTurnoSolicitadaSerializacionTests,
    // precedente #288/#319).
    public SedeProgramada? Sede { get; private set; }

    public ProgramacionTurnoSolicitada(
        Guid id,
        ColaboradorProgramado empleado,
        IReadOnlyList<DateOnly> fechas,
        TurnoProgramado detalleTurno,
        SedeProgramada? sede = null)
    {
        Id = id;
        Empleado = empleado;
        Fechas = fechas;
        DetalleTurno = detalleTurno;
        Sede = sede;
    }

    // Constructor para Marten/serializacion
    private ProgramacionTurnoSolicitada() { }
}
