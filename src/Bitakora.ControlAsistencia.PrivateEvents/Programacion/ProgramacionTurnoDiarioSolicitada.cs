using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.PrivateEvents.Programacion;

/// <summary>
/// Evento privado intra-BC que se publica al namespace interno del Bounded Context
/// via IPrivateEventSender (ADR-0024 decision #3: todo evento privado cruza fisicamente el ASB).
/// Se emite uno por cada fecha del arreglo del comando.
/// Consumidor: ControlHoras (mismo Bounded Context "Control de Asistencia") -> es intra-BC,
/// por eso es IPrivateEvent y no IPublicEvent (ADR-0024 decision #2).
/// Todo su payload es propio de este ensamblado -- DetalleColaborador incluido, que duplica a
/// InformacionColaborador (PublicEvents) en vez de importarlo (CA-ADR-0029 decisiones #2 y #5).
/// </summary>
public sealed class ProgramacionTurnoDiarioSolicitada : IPrivateEvent
{
    public Guid SolicitudId { get; private set; }
    public DetalleColaborador Colaborador { get; private set; } = null!;
    public DateOnly Fecha { get; private set; }
    public DetalleTurno DetalleTurno { get; private set; } = null!;

    // Issue #331: sede efectiva del dia, opcional (null = sin sede asignada). Campo aditivo y
    // tolerante: los mensajes publicados antes de este issue no llevan la clave "sede" en el JSON
    // del bus; STJ deja null en el parametro posicional opcional (precedente Descripcion, #288).
    public DetalleSede? Sede { get; private set; }

    public ProgramacionTurnoDiarioSolicitada(
        Guid solicitudId,
        DetalleColaborador colaborador,
        DateOnly fecha,
        DetalleTurno detalleTurno,
        DetalleSede? sede = null)
    {
        SolicitudId = solicitudId;
        Colaborador = colaborador;
        Fecha = fecha;
        DetalleTurno = detalleTurno;
        Sede = sede;
    }

    // Constructor para Marten/serializacion
    private ProgramacionTurnoDiarioSolicitada() { }
}
