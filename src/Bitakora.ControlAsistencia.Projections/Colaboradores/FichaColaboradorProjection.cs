using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.Colaboradores;
using JasperFx.Events; // IEvent<T> vive aqui, NO en Marten.Events (MEF-ADR-0034 seccion 6)
using Marten.Events.Aggregation; // SingleStreamProjection<,> vive aqui, NO en Marten.Events.Projections

namespace Bitakora.ControlAsistencia.Projections.Colaboradores;

/// <summary>
/// Clase de proyeccion companion de FichaColaborador (issue #356, receta N1 -- un solo stream, el
/// del colaborador; MEF-ADR-0035). Vive en el worker (Bitakora.ControlAsistencia.Projections), el
/// ensamblado que si referencia Marten y el analizador JasperFx.Events.SourceGenerator.
///
/// partial es obligatorio (skills/projections/modelos-marten.md): el source generator descubre
/// Create/Apply por convencion y emite el dispatcher [GeneratedEvolver]. Sin partial el build queda
/// limpio pero falla en RUNTIME al registrar la proyeccion (InvalidProjectionException) -- error
/// que el config-test detecta al resolver el named store
/// (ConfigurarColaboradores_RegistraFichaColaboradorProjectionComoAsync).
///
/// Se registra en ConfiguracionMartenProjectionsColaboradores.ConfigurarColaboradores con
/// opts.Projections.Add&lt;FichaColaboradorProjection&gt;(ProjectionLifecycle.Async) -- lifecycle
/// canonico del worker (MEF-ADR-0034 seccion 3). Ese seam ya existe (issue #360) y no se toca desde
/// esta fase roja: la linea de registro es responsabilidad de projection-implementer.
///
/// Create toma IEvent&lt;ColaboradorRegistrado&gt;, no ColaboradorRegistrado a secas: la identidad
/// del documento (un string, "{Tipo}:{Numero}") es exactamente el StreamKey del stream de
/// ColaboradorAggregateRoot (Events.StreamIdentity = AsString) -- IEvent&lt;T&gt;.StreamKey es quien
/// la expone, sin recomputarla a mano desde el payload (mismo criterio que
/// skills/projections/modelos-marten.md, ejemplo SeguimientoTurnoProjection.Create).
///
/// Sin ShouldDelete: la ficha nunca se borra (issue #356, "Receta").
/// </summary>
public sealed partial class FichaColaboradorProjection : SingleStreamProjection<FichaColaborador, string>
{
    public static FichaColaborador Create(IEvent<ColaboradorRegistrado> e) =>
        throw new NotImplementedException();

    // CA-1 (segunda mitad) / CA-5: codigo y VigenteDesde nuevos, VigenteHasta al centinela y AMBAS
    // estructuras de etiquetas vaciadas -- "reingreso nace limpio" (espejo de Apply(VinculacionIniciada)
    // en ColaboradorAggregateRoot, #355 CA-6). En el registro inicial (mismo commit que
    // ColaboradorRegistrado) el vaciado es inocuo: la ficha todavia no tiene etiquetas.
    public static FichaColaborador Apply(VinculacionIniciada e, FichaColaborador vista) =>
        throw new NotImplementedException();

    // CA-2 (primera mitad): VigenteHasta = FechaEfectiva.
    public static FichaColaborador Apply(VinculacionTerminada e, FichaColaborador vista) =>
        throw new NotImplementedException();

    // CA-2 (segunda mitad): reabre -- VigenteHasta vuelve al centinela.
    public static FichaColaborador Apply(TerminacionAnulada e, FichaColaborador vista) =>
        throw new NotImplementedException();

    // CA-3 (primera mitad): reemplaza NombreCompleto.
    public static FichaColaborador Apply(NombresCorregidos e, FichaColaborador vista) =>
        throw new NotImplementedException();

    // CA-3 (segunda mitad): reemplaza VigenteDesde.
    public static FichaColaborador Apply(FechaInicioVinculacionCorregida e, FichaColaborador vista) =>
        throw new NotImplementedException();

    // CA-4 (primera mitad): upsert en AMBAS estructuras por categoria normalizada -- un valor por
    // categoria (espejo de la invariante de EtiquetaAsignada, #355 CA-2: el evento siempre
    // representa el estado final de esa categoria).
    public static FichaColaborador Apply(EtiquetaAsignada e, FichaColaborador vista) =>
        throw new NotImplementedException();

    // CA-4 (segunda mitad): remueve la categoria de ambas estructuras.
    public static FichaColaborador Apply(EtiquetaRetirada e, FichaColaborador vista) =>
        throw new NotImplementedException();
}
