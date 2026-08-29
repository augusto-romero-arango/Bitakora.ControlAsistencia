using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using JasperFx.Events; // IEvent<T> vive aqui, NO en Marten.Events (MEF-ADR-0034 seccion 6)
using Marten.Events.Aggregation; // SingleStreamProjection<,> vive aqui, NO en Marten.Events.Projections

namespace Bitakora.ControlAsistencia.Projections.Programacion;

/// <summary>
/// Clase de proyeccion companion de FichaTurno (issue #496, receta N1 -- un solo stream, el del
/// catalogo de turnos CatalogoTurnos; MEF-ADR-0035). Vive en el worker
/// (Bitakora.ControlAsistencia.Projections), el ensamblado que si referencia Marten y el analizador
/// JasperFx.Events.SourceGenerator.
/// </summary>
/// <remarks>
/// partial es obligatorio (skills/projections/modelos-marten.md): el source generator descubre
/// Create/Apply por convencion y emite el dispatcher [GeneratedEvolver]. Sin partial el build queda
/// limpio pero falla en RUNTIME al registrar la proyeccion (InvalidProjectionException).
///
/// Se registra en ConfiguracionMartenProjectionsProgramacion.ConfigurarProgramacion con
/// opts.Projections.Add&lt;FichaTurnoProjection&gt;(ProjectionLifecycle.Async) -- pendiente,
/// responsabilidad de projection-implementer (issue #496, CA-5). Ese registro es ademas lo que hace
/// que Marten aplique ProjectionDocumentPolicy sobre FichaTurno (mt_version bigint): el Function App
/// de Programacion que la consulta (ObtenerFichaTurno, ListarFichasTurno) debe declarar la misma
/// forma con Schema.For&lt;FichaTurno&gt;().UseNumericRevisions(true).
///
/// Create toma IEvent&lt;TurnoCreado&gt;, no TurnoCreado a secas: la identidad del documento
/// (FichaTurno.Id) es exactamente el StreamKey del stream de CatalogoTurnos (Events.StreamIdentity
/// = AsString) -- IEvent&lt;T&gt;.StreamKey es quien la expone, sin recomputarla a mano desde el
/// payload (mismo criterio que skills/projections/modelos-marten.md).
///
/// Sin Apply/ShouldDelete: TurnoCreado es el UNICO evento que CatalogoTurnos declara hoy -- la
/// ficha nunca cambia despues de creada.
///
/// El mapeo evento -&gt; vista delega en FranjaOrdinaria.ToDetalle()/SubFranja.ToDetalle() -- el DTO
/// plano ya expuesto por el VO rico (Tell-don't-Ask, MEF-ADR-0012) -- en vez de reabrir campos
/// privados de FranjaOrdinaria/SubFranja: FranjaProgramada.Descripcion YA es el mismo texto que
/// FranjaOrdinaria.ToString() produce ("(06:00-14:00)[Descansos:...][Extras:...][sede:...]"), asi
/// que se reusa tal cual para FichaTurno.Descripcion (y para cada FranjaFicha.Descripcion) en vez
/// de recomponerlo aqui.
///
/// Decision propia de este projection-implementer, sin CA que la fije (issue #496, "Notas
/// tecnicas": "Ningun CA fija el algoritmo exacto de HorarioResumido/Descripcion"): con una unica
/// franja HorarioResumido es el rango corto "HH:mm-HH:mm" de esa franja (sin offset de dia, sin
/// descansos/extras/sede -- es la confirmacion RAPIDA); con varias franjas se unen con ", " en el
/// mismo formato corto. Multi-franja no tiene test propio en FichaTurnoProjectionTests -- el unico
/// escenario cubierto (CA-1) trae una sola franja.
/// </remarks>
public sealed partial class FichaTurnoProjection : SingleStreamProjection<FichaTurno, string>
{
    public static FichaTurno Create(IEvent<TurnoCreado> e)
    {
        var turnoCreado = e.Data;

        // CA-2: variante descanso (factory CrearDescanso) -- sin franjas, EsDescanso = true.
        if (turnoCreado.FranjasOrdinarias.Count == 0)
            return new FichaTurno(e.StreamKey!, turnoCreado.Nombre, true, "Descanso", [], "Descanso");

        var detalles = turnoCreado.FranjasOrdinarias.Select(f => f.ToDetalle()).ToList();

        var horarioResumido = string.Join(", ",
            detalles.Select(d => $"{d.HoraInicio:HH\\:mm}-{d.HoraFin:HH\\:mm}"));
        var descripcion = string.Join(", ", detalles.Select(d => d.Descripcion));

        return new FichaTurno(
            e.StreamKey!,
            turnoCreado.Nombre,
            false,
            horarioResumido,
            detalles.Select(MapearFranja).ToList(),
            descripcion);
    }

    private static FranjaFicha MapearFranja(FranjaProgramada detalle) =>
        new(
            detalle.HoraInicio,
            detalle.HoraFin,
            detalle.DiaOffsetFin,
            detalle.Descansos.Select(MapearSubFranja).ToList(),
            detalle.Extras.Select(MapearSubFranja).ToList(),
            detalle.Sede?.Id,
            detalle.Sede?.Nombre,
            detalle.Descripcion);

    private static SubFranjaFicha MapearSubFranja(SubFranjaProgramada detalle) =>
        new(detalle.HoraInicio, detalle.HoraFin, detalle.DiaOffsetInicio, detalle.DiaOffsetFin);
}
