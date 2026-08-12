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
/// canonico del worker (MEF-ADR-0034 seccion 3). Ese registro es ademas lo que hace que Marten
/// aplique ProjectionDocumentPolicy sobre FichaColaborador (mt_version bigint): el Function App que
/// la consulta debe declarar la misma forma con Schema.For&lt;FichaColaborador&gt;()
/// .UseNumericRevisions(true), y el par de config-tests de ambos lados congela esos literales.
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
    // El centinela de vigencia abierta (issue #356, "Vista a materializar") vive con la vista, no
    // aqui: es la codificacion de uno de SUS campos y el endpoint ObtenerFichaColaborador -- en
    // otro proceso, sin referencia a este ensamblado -- tiene que leer exactamente el mismo valor
    // para traducirlo a vacio antes de responder (CA-6). Ver FichaColaborador.CentinelaVigenciaAbierta.

    // CA-1 (primera mitad): la ficha nace con Id (StreamKey del stream de ColaboradorAggregateRoot)
    // y NombreCompleto -- el resto de los campos queda en su forma "vacia" hasta que
    // Apply(VinculacionIniciada) los complete (mismo commit, ver abajo).
    public static FichaColaborador Create(IEvent<ColaboradorRegistrado> e) =>
        new(
            e.StreamKey!,
            e.Data.Nombre.NombreCompleto,
            string.Empty,
            default,
            FichaColaborador.CentinelaVigenciaAbierta,
            [],
            new Dictionary<string, string>());

    // CA-1 (segunda mitad) / CA-5: codigo y VigenteDesde nuevos, VigenteHasta al centinela y AMBAS
    // estructuras de etiquetas vaciadas -- "reingreso nace limpio" (espejo de Apply(VinculacionIniciada)
    // en ColaboradorAggregateRoot, #355 CA-6). En el registro inicial (mismo commit que
    // ColaboradorRegistrado) el vaciado es inocuo: la ficha todavia no tiene etiquetas.
    public static FichaColaborador Apply(VinculacionIniciada e, FichaColaborador vista) =>
        vista with
        {
            CodigoColaborador = e.Codigo,
            VigenteDesde = e.FechaInicio,
            VigenteHasta = FichaColaborador.CentinelaVigenciaAbierta,
            Etiquetas = [],
            EtiquetasNormalizadas = new Dictionary<string, string>()
        };

    // CA-2 (primera mitad): VigenteHasta = FechaEfectiva.
    public static FichaColaborador Apply(VinculacionTerminada e, FichaColaborador vista) =>
        vista with { VigenteHasta = e.FechaEfectiva };

    // CA-2 (segunda mitad): reabre -- VigenteHasta vuelve al centinela.
    public static FichaColaborador Apply(TerminacionAnulada e, FichaColaborador vista) =>
        vista with { VigenteHasta = FichaColaborador.CentinelaVigenciaAbierta };

    // CA-3 (primera mitad): reemplaza NombreCompleto.
    public static FichaColaborador Apply(NombresCorregidos e, FichaColaborador vista) =>
        vista with { NombreCompleto = e.Nombre.NombreCompleto };

    // CA-3 (segunda mitad): reemplaza VigenteDesde.
    public static FichaColaborador Apply(FechaInicioVinculacionCorregida e, FichaColaborador vista) =>
        vista with { VigenteDesde = e.FechaInicio };

    // CA-4 (primera mitad): upsert en AMBAS estructuras por categoria normalizada -- un valor por
    // categoria (espejo de la invariante de EtiquetaAsignada, #355 CA-2: el evento siempre
    // representa el estado final de esa categoria). Tell-don't-Ask (MEF-ADR-0012): la normalizacion
    // de la categoria de cada EtiquetaFicha existente se recalcula via Etiqueta.NormalizarCategoria
    // (VO de DomainEvents) en vez de reimplementarla aqui; e.Etiqueta ya trae su propia forma
    // normalizada, persistida por el aggregate.
    public static FichaColaborador Apply(EtiquetaAsignada e, FichaColaborador vista)
    {
        var etiquetas = vista.Etiquetas
            .Where(existente => Etiqueta.NormalizarCategoria(existente.Categoria) != e.Etiqueta.CategoriaNormalizada)
            .Append(new EtiquetaFicha(e.Etiqueta.Categoria, e.Etiqueta.Valor))
            .ToList();

        var normalizadas = new Dictionary<string, string>(vista.EtiquetasNormalizadas)
        {
            [e.Etiqueta.CategoriaNormalizada] = e.Etiqueta.ValorNormalizado
        };

        return vista with { Etiquetas = etiquetas, EtiquetasNormalizadas = normalizadas };
    }

    // CA-4 (segunda mitad): remueve la categoria de ambas estructuras.
    public static FichaColaborador Apply(EtiquetaRetirada e, FichaColaborador vista)
    {
        var etiquetas = vista.Etiquetas
            .Where(existente => Etiqueta.NormalizarCategoria(existente.Categoria) != e.CategoriaNormalizada)
            .ToList();

        var normalizadas = new Dictionary<string, string>(vista.EtiquetasNormalizadas);
        normalizadas.Remove(e.CategoriaNormalizada);

        return vista with { Etiquetas = etiquetas, EtiquetasNormalizadas = normalizadas };
    }
}
