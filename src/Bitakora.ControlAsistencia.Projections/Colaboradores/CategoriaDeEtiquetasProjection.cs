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

    // Stub -- fase roja read-side (issue #357). La implementacion real (nace el documento con la
    // categoria y su primer valor) la escribe projection-implementer.
    public static CategoriaDeEtiquetas Create(EtiquetaAsignada e) =>
        throw new NotImplementedException();

    // Stub -- fase roja read-side (issue #357). La implementacion real (agrupar por categoria,
    // ultima-gana en Categoria y en cada Valor, acumulacion sin duplicados por ValorNormalizado) la
    // escribe projection-implementer.
    public static CategoriaDeEtiquetas Apply(EtiquetaAsignada e, CategoriaDeEtiquetas vista) =>
        throw new NotImplementedException();
}
