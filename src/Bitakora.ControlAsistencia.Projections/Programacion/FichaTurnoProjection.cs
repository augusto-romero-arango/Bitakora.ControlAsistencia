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
/// STUB de fase roja (projection-test-writer, issue #496): Create lanza NotImplementedException a
/// proposito. El mapeo real de franjas y la composicion de HorarioResumido/Descripcion son
/// responsabilidad de projection-implementer -- ver
/// Bitakora.ControlAsistencia.Projections.Tests.Programacion.FichaTurnoProjectionTests para el
/// contrato exacto que debe satisfacer.
/// </remarks>
public sealed partial class FichaTurnoProjection : SingleStreamProjection<FichaTurno, string>
{
    public static FichaTurno Create(IEvent<TurnoCreado> e) => throw new NotImplementedException();
}
