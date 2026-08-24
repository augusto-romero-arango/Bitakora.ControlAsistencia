using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.PrivateEvents.Programacion;

/// <summary>
/// Su unico consumidor, ControlHoras, vive en este mismo Bounded Context: es IPrivateEvent y no
/// IPublicEvent (ADR-0024 decision #2). Todo su payload es propio de PrivateEvents -- nada
/// importado de PublicEvents ni de un {Dominio}.DomainEvents, incluido ResumenColaborador, que
/// vive en el namespace Colaboradores de este mismo ensamblado (CA-ADR-0029 decisiones #2 y #5,
/// MEF-ADR-0039 decision 2).
///
/// Colaborador es la terna de identidad, no el quinteto de InformacionColaborador (PublicEvents):
/// la asimetria es deliberada y no se "corrige" restaurando la paridad de campos -- la terna se
/// COMPONE desde el quinteto en el productor, no lo espeja.
/// </summary>
public sealed class ProgramacionTurnoDiarioSolicitada : IPrivateEvent
{
    public Guid SolicitudId { get; private set; }
    public ResumenColaborador Colaborador { get; private set; } = null!;
    public DateOnly Fecha { get; private set; }
    public DetalleTurno DetalleTurno { get; private set; } = null!;

    // Sede efectiva del dia; null = sin sede asignada, valor valido. Debe seguir siendo el ultimo
    // parametro posicional y opcional: los mensajes viejos del bus no llevan la clave "sede" y STJ
    // solo deja null si el parametro lo admite.
    public DetalleSede? Sede { get; private set; }

    public ProgramacionTurnoDiarioSolicitada(
        Guid solicitudId,
        ResumenColaborador colaborador,
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
