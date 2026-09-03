using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.Colaboradores;
using JasperFx.Events; // IEvent<T> vive aqui, NO en Marten.Events (MEF-ADR-0034 seccion 6)
using Marten.Events.Aggregation; // SingleStreamProjection<,> vive aqui, NO en Marten.Events.Projections

namespace Bitakora.ControlAsistencia.Projections.Colaboradores;

/// <summary>
/// Clase de proyeccion companion de DirectorioColaborador (issue #587, receta N1 -- un solo stream,
/// el del colaborador, mismo stream que FichaColaboradorProjection; MEF-ADR-0035). Vive en el worker
/// (Bitakora.ControlAsistencia.Projections), el ensamblado que si referencia Marten y el analizador
/// JasperFx.Events.SourceGenerator.
///
/// partial es obligatorio (skills/projections/modelos-marten.md): el source generator descubre
/// Create/Apply por convencion y emite el dispatcher [GeneratedEvolver]. Sin partial el build queda
/// limpio pero falla en RUNTIME al registrar la proyeccion (InvalidProjectionException).
///
/// Se registra en ConfiguracionMartenProjectionsColaboradores.ConfigurarColaboradores con
/// opts.Projections.Add&lt;DirectorioColaboradorProjection&gt;(ProjectionLifecycle.Async) --
/// lifecycle canonico del worker (MEF-ADR-0034 seccion 3).
///
/// Create toma IEvent&lt;ColaboradorRegistrado&gt;, no ColaboradorRegistrado a secas: la identidad
/// del documento es exactamente el StreamKey del stream de ColaboradorAggregateRoot --
/// IEvent&lt;T&gt;.StreamKey es quien la expone, sin recomputarla a mano desde el payload (mismo
/// criterio que FichaColaboradorProjection.Create).
///
/// Solo consume los 7 eventos que el issue #587 enumera -- ningun Apply para
/// EtiquetaAsignada/EtiquetaRetirada: el directorio no lleva etiquetas (esa busqueda sigue siendo
/// del QUERY de fichas).
///
/// Sin ShouldDelete: el directorio nunca borra (issue #587, "Receta").
/// </summary>
public sealed partial class DirectorioColaboradorProjection
    : SingleStreamProjection<DirectorioColaborador, string>
{
    // CA-1: nace con Id, TipoDocumento, NumeroDocumento (normalizado), NombreCompleto y TokensNombre
    // -- el resto queda en su forma "vacia" hasta que Apply(VinculacionIniciada) los completa (mismo
    // commit que ColaboradorRegistrado, misma forma que FichaColaboradorProjection.Create).
    public static DirectorioColaborador Create(IEvent<ColaboradorRegistrado> e) =>
        throw new NotImplementedException();

    // CA-1 (segunda mitad) / CA-2: codigo y VigenteDesde nuevos, VigenteHasta al centinela y
    // CodigoSede = e.CodigoSede tal cual -- "reingreso nace limpio" (espejo de
    // FichaColaboradorProjection.Apply(VinculacionIniciada) y de
    // ColaboradorAggregateRoot.Apply(VinculacionIniciada), #520).
    public static DirectorioColaborador Apply(VinculacionIniciada e, DirectorioColaborador vista) =>
        throw new NotImplementedException();

    // CA-2: VigenteHasta = FechaEfectiva.
    public static DirectorioColaborador Apply(VinculacionTerminada e, DirectorioColaborador vista) =>
        throw new NotImplementedException();

    // CA-2: reabre -- VigenteHasta vuelve al centinela.
    public static DirectorioColaborador Apply(TerminacionAnulada e, DirectorioColaborador vista) =>
        throw new NotImplementedException();

    // CA-3: reemplaza NombreCompleto Y recalcula TokensNombre (Tell-don't-Ask, MEF-ADR-0012: invoca
    // DirectorioColaborador.TokenizarNombre, ningun algoritmo propio aqui).
    public static DirectorioColaborador Apply(NombresCorregidos e, DirectorioColaborador vista) =>
        throw new NotImplementedException();

    // CA-2: reemplaza VigenteDesde.
    public static DirectorioColaborador Apply(FechaInicioVinculacionCorregida e, DirectorioColaborador vista) =>
        throw new NotImplementedException();

    // CA-3: reemplaza CodigoSede -- SedeAsignada representa siempre el reemplazo completo de la sede
    // (primera asignacion y reasignacion emiten el mismo evento, sin evento de retiro).
    public static DirectorioColaborador Apply(SedeAsignada e, DirectorioColaborador vista) =>
        throw new NotImplementedException();
}
