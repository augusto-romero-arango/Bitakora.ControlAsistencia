using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using Marten.Events.Aggregation; // SingleStreamProjection<,> vive aqui, NO en Marten.Events.Projections

namespace Bitakora.ControlAsistencia.Projections.ControlHoras;

/// <summary>
/// Clase de proyeccion companion de TurnoDiarioView (issue #289, receta N1 -- un solo stream,
/// (EmpleadoId, Fecha), MEF-ADR-0035). Vive en el worker (Bitakora.ControlAsistencia.Projections),
/// el ensamblado que si referencia Marten y el analizador JasperFx.Events.SourceGenerator.
///
/// partial es obligatorio (skills/projections/modelos-marten.md): el source generator descubre
/// Create/Apply por convencion y emite el dispatcher [GeneratedEvolver]. Sin partial el build
/// queda limpio pero falla en RUNTIME al registrar la proyeccion (InvalidProjectionException) --
/// error que el config-test ya detecta (ConfigurarControlHoras_ResuelveElNamedStoreDelDominio).
///
/// FASE ROJA (projection-test-writer): Create/Apply son stubs. projection-implementer los completa
/// y ademas registra esta clase en ConfiguracionMartenProjectionsControlHoras.ConfigurarControlHoras
/// con opts.Projections.Add&lt;TurnoDiarioProjection&gt;(ProjectionLifecycle.Async) -- ese registro
/// es el rojo que cubre ConfiguracionMartenProjectionsTests.
///
/// Solo TurnoDiarioAsignado alimenta esta vista: MarcacionAdicionada tambien vive en el mismo
/// stream de ControlDiarioAggregateRoot pero esta proyeccion la ignora a proposito (CA-2). Sin
/// ShouldDelete: el turno diario nunca se borra, solo se reasigna (CA-3, "el ultimo gana").
/// </summary>
public sealed partial class TurnoDiarioProjection : SingleStreamProjection<TurnoDiarioView, string>
{
    public static TurnoDiarioView Create(TurnoDiarioAsignado evento) =>
        new(evento.Id, evento.InformacionEmpleado, evento.Fecha, evento.DetalleTurno, evento.SolicitudId);

    // CA-3: "el ultimo gana" -- una reasignacion sobre el mismo (empleado, fecha) sobrescribe el
    // documento completo con lo que trae el nuevo evento. El Id no cambia (mismo stream key).
    public static TurnoDiarioView Apply(TurnoDiarioAsignado evento, TurnoDiarioView vista) =>
        vista with
        {
            Empleado = evento.InformacionEmpleado,
            Fecha = evento.Fecha,
            DetalleTurno = evento.DetalleTurno,
            UltimaSolicitudId = evento.SolicitudId
        };
}
