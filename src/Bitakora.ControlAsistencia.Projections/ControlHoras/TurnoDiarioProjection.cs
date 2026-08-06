using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.PublicEvents.Empleados;
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
/// Se registra en ConfiguracionMartenProjectionsControlHoras.ConfigurarControlHoras con
/// opts.Projections.Add&lt;TurnoDiarioProjection&gt;(ProjectionLifecycle.Async) -- lifecycle canonico
/// del worker (MEF-ADR-0034 seccion 3), verificado por ConfiguracionMartenProjectionsTests.
///
/// Solo TurnoDiarioAsignado alimenta esta vista: MarcacionAdicionada tambien vive en el mismo
/// stream de ControlDiarioAggregateRoot pero esta proyeccion la ignora a proposito (CA-2). Sin
/// ShouldDelete: el turno diario nunca se borra, solo se reasigna (CA-3, "el ultimo gana").
/// </summary>
// Issue #322: TurnoDiarioAsignado ahora persiste Empleado/TurnoDiario (ControlHoras.DomainEvents,
// payload por rol) en vez de InformacionEmpleado/DetalleTurno (PublicEvents/PrivateEvents).
// TurnoDiarioView NO cambia en este issue (estado intermedio deliberado, ver issue #322 "Notas
// tecnicas"): sigue usando los tipos de bus, asi que Create/Apply mapean los records del evento a
// esos tipos -- mapeo mecanico dirigido por el compilador, no una vista nueva.
public sealed partial class TurnoDiarioProjection : SingleStreamProjection<TurnoDiarioView, string>
{
    public static TurnoDiarioView Create(TurnoDiarioAsignado evento) =>
        new(evento.Id, MapearEmpleado(evento.InformacionEmpleado), evento.Fecha,
            MapearDetalleTurno(evento.DetalleTurno), evento.SolicitudId);

    // CA-3: "el ultimo gana" -- una reasignacion sobre el mismo (empleado, fecha) sobrescribe el
    // documento completo con lo que trae el nuevo evento. El Id no cambia (mismo stream key).
    public static TurnoDiarioView Apply(TurnoDiarioAsignado evento, TurnoDiarioView vista) =>
        vista with
        {
            Empleado = MapearEmpleado(evento.InformacionEmpleado),
            Fecha = evento.Fecha,
            DetalleTurno = MapearDetalleTurno(evento.DetalleTurno),
            UltimaSolicitudId = evento.SolicitudId
        };

    private static InformacionEmpleado MapearEmpleado(Empleado empleado) =>
        new(empleado.EmpleadoId, empleado.TipoIdentificacion, empleado.NumeroIdentificacion,
            empleado.Nombres, empleado.Apellidos);

    private static DetalleTurno MapearDetalleTurno(TurnoDiario turno) =>
        new(turno.Nombre, turno.FranjasOrdinarias.Select(MapearFranja).ToList(), turno.Descripcion);

    private static DetalleFranjaOrdinaria MapearFranja(FranjaProgramada franja) =>
        new(franja.HoraInicio, franja.HoraFin, franja.DiaOffsetFin,
            franja.Descansos.Select(MapearSubFranja).ToList(),
            franja.Extras.Select(MapearSubFranja).ToList(),
            franja.Descripcion);

    private static DetalleSubFranja MapearSubFranja(SubFranjaProgramada sub) =>
        new(sub.HoraInicio, sub.HoraFin, sub.DiaOffsetInicio, sub.DiaOffsetFin, sub.Descripcion);
}
