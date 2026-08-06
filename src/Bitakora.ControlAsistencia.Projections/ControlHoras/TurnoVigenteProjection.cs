using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using Marten.Events.Aggregation; // SingleStreamProjection<,> vive aqui, NO en Marten.Events.Projections

namespace Bitakora.ControlAsistencia.Projections.ControlHoras;

/// <summary>
/// Clase de proyeccion companion de TurnoVigente (issue #328, receta N1 -- un solo stream,
/// (EmpleadoId, Fecha), MEF-ADR-0035). Vive en el worker (Bitakora.ControlAsistencia.Projections),
/// el ensamblado que si referencia Marten y el analizador JasperFx.Events.SourceGenerator.
///
/// partial es obligatorio (skills/projections/modelos-marten.md): el source generator descubre
/// Create/Apply por convencion y emite el dispatcher [GeneratedEvolver]. Sin partial el build
/// queda limpio pero falla en RUNTIME al registrar la proyeccion (InvalidProjectionException) --
/// error que el config-test detecta al resolver el named store
/// (ConfigurarControlHoras_RegistraTurnoVigenteProjectionComoAsync).
///
/// FASE ROJA (projection-test-writer, issue #328): Create/Apply son stubs que lanzan
/// NotImplementedException a proposito -- la implementacion real (invocar
/// evento.DetalleTurno.Segmentar(evento.Fecha) y mapear cada BloqueTurno a Bloque, Tell-don't-Ask
/// MEF-ADR-0012) es responsabilidad de projection-implementer. El registro en el named store del
/// worker (ConfiguracionMartenProjectionsControlHoras.ConfigurarControlHoras,
/// opts.Projections.Add&lt;TurnoVigenteProjection&gt;(ProjectionLifecycle.Async)) tampoco se toca
/// aqui: ese seam ya existe con implementacion real (issue #289) y sumar esta proyeccion es
/// tambien alcance de projection-implementer.
///
/// Solo TurnoDiarioAsignado alimenta esta vista: MarcacionAdicionada tambien vive en el mismo
/// stream de ControlDiarioAggregateRoot pero esta proyeccion la ignora a proposito (issue #328,
/// "Eventos que la alimentan"). Sin ShouldDelete: el turno vigente nunca se borra, solo se
/// reasigna ("el ultimo gana", CA-2).
/// </summary>
public sealed partial class TurnoVigenteProjection : SingleStreamProjection<TurnoVigente, string>
{
    public static TurnoVigente Create(TurnoDiarioAsignado evento) =>
        throw new NotImplementedException();

    public static TurnoVigente Apply(TurnoDiarioAsignado evento, TurnoVigente vista) =>
        throw new NotImplementedException();
}
