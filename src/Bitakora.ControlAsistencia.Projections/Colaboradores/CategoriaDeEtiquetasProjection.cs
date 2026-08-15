using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.Colaboradores;
using Marten.Events.Projections; // MultiStreamProjection<,> vive aqui, NO en Marten.Events.Aggregation

namespace Bitakora.ControlAsistencia.Projections.Colaboradores;

/// <summary>
/// Clase de proyeccion companion de CategoriaDeEtiquetas (issue #357, receta N2 --
/// MultiStreamProjection&lt;CategoriaDeEtiquetas, string&gt;: eventos EtiquetaAsignada de MUCHOS
/// streams de ColaboradorAggregateRoot convergen en el MISMO documento cuando comparten categoria
/// normalizada; MEF-ADR-0035, skills/projections/modelos-marten.md). Es la PRIMERA proyeccion N2 de
/// este BC -- FichaColaboradorProjection (#356) y TurnoVigenteProjection (ControlHoras) son N1.
///
/// partial es obligatorio (mismo gotcha que FichaColaboradorProjection, #356): el source generator
/// descubre Create/Apply por convencion y emite el dispatcher [GeneratedEvolver]. Sin partial el
/// build queda limpio pero falla en RUNTIME al registrar la proyeccion (InvalidProjectionException),
/// error que el config-test detecta al resolver el named store
/// (ConfigurarColaboradores_RegistraCategoriaDeEtiquetasProjectionComoAsync).
///
/// Se registra en ConfiguracionMartenProjectionsColaboradores.ConfigurarColaboradores con
/// opts.Projections.Add&lt;CategoriaDeEtiquetasProjection&gt;(ProjectionLifecycle.Async) -- lifecycle
/// canonico del worker (MEF-ADR-0034 seccion 3).
///
/// CATALOGO ACUMULATIVO (CA-5, decision de refinamiento 2026-08-13): esta clase NO declara ningun
/// metodo para EtiquetaRetirada -- ese evento no descuenta nada del catalogo (la sobrescritura y el
/// reingreso-nace-limpio son retiros implicitos que exigirian estado por colaborador, territorio N3
/// sin beneficio hoy para el proposito del autocompletado). Es una garantia ESTRUCTURAL (ausencia de
/// metodo, no logica que testear): CategoriaDeEtiquetasProjectionTests documenta la regla en un
/// comentario, no en un [Fact], porque un test que solo reflexionara sobre esa ausencia pasaria de
/// una contra el stub -- nada que implementar lo pondria en rojo.
/// </summary>
public sealed partial class CategoriaDeEtiquetasProjection : MultiStreamProjection<CategoriaDeEtiquetas, string>
{
    public CategoriaDeEtiquetasProjection()
    {
        // Correlacion N2 (skills/projections/modelos-marten.md, "N2 -- correlacion entre streams"):
        // la identidad del documento es la categoria NORMALIZADA del payload de EtiquetaAsignada --
        // no el StreamKey de ningun stream de ColaboradorAggregateRoot. No es un stub: es codigo de
        // configuracion real, sin logica de negocio que testear (por eso no hay unit test dedicado a
        // esta linea; el test de correlacion de CategoriaDeEtiquetasProjectionTests verifica su
        // EFECTO -- que dos streams distintos convergen en el mismo documento -- invocando Create/
        // Apply directamente).
        Identity<EtiquetaAsignada>(e => e.Etiqueta.CategoriaNormalizada);
    }

    // CA-1: el documento nace con la categoria NORMALIZADA como Id (misma identidad que fija el
    // slicer de correlacion arriba), el display de esa PRIMERA asignacion y su primer valor
    // (display + normalizado, ambos leidos del VO Etiqueta -- sin recalcular nada, MEF-ADR-0012).
    public static CategoriaDeEtiquetas Create(EtiquetaAsignada e) =>
        new(
            e.Etiqueta.CategoriaNormalizada,
            e.Etiqueta.Categoria,
            [new ValorCategoria(e.Etiqueta.Valor, e.Etiqueta.ValorNormalizado)]);

    // CA-2/CA-3/CA-4: "la ultima gana" en el display de la categoria (siempre se reemplaza,
    // sin importar la forma original de esta asignacion) y upsert por ValorNormalizado en
    // Valores -- se descarta el valor existente con el MISMO ValorNormalizado (si lo hay) y se
    // agrega el nuevo, conservando el resto: acumulativo (CATALOGO ACUMULATIVO, decision de
    // refinamiento 2026-08-13), nunca un reemplazo total de la lista.
    public static CategoriaDeEtiquetas Apply(EtiquetaAsignada e, CategoriaDeEtiquetas vista)
    {
        var valores = vista.Valores
            .Where(existente => existente.ValorNormalizado != e.Etiqueta.ValorNormalizado)
            .Append(new ValorCategoria(e.Etiqueta.Valor, e.Etiqueta.ValorNormalizado))
            .ToList();

        return vista with { Categoria = e.Etiqueta.Categoria, Valores = valores };
    }
}
