using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.Colaboradores;
using Marten.Events.Projections; // MultiStreamProjection<,> vive aqui, NO en Marten.Events.Aggregation

namespace Bitakora.ControlAsistencia.Projections.Colaboradores;

/// <summary>
/// Proyeccion N2: EtiquetaAsignada de muchos streams converge en el mismo documento cuando comparte
/// categoria normalizada (MEF-ADR-0035).
///
/// partial es obligatorio: el source generator descubre Create/Apply por convencion y emite el
/// dispatcher [GeneratedEvolver]. Sin partial el build queda limpio y falla en RUNTIME al registrar la
/// proyeccion (InvalidProjectionException).
///
/// Catalogo ACUMULATIVO: a proposito no hay metodo para EtiquetaRetirada. Descontar exigiria estado
/// por colaborador (sobrescritura y reingreso son retiros implicitos), sin beneficio para el
/// autocompletado.
/// </summary>
public sealed partial class CategoriaDeEtiquetasProjection : MultiStreamProjection<CategoriaDeEtiquetas, string>
{
    public CategoriaDeEtiquetasProjection()
    {
        // La identidad es la categoria NORMALIZADA del payload, no el StreamKey de ningun stream.
        Identity<EtiquetaAsignada>(e => e.Etiqueta.CategoriaNormalizada);
    }

    public static CategoriaDeEtiquetas Create(EtiquetaAsignada e) =>
        new(
            e.Etiqueta.CategoriaNormalizada,
            e.Etiqueta.Categoria,
            [new ValorCategoria(e.Etiqueta.Valor, e.Etiqueta.ValorNormalizado)]);

    // "La ultima gana" en el display de la categoria; Valores es upsert por ValorNormalizado, nunca un
    // reemplazo total de la lista (catalogo acumulativo).
    public static CategoriaDeEtiquetas Apply(EtiquetaAsignada e, CategoriaDeEtiquetas vista)
    {
        var valores = vista.Valores
            .Where(existente => existente.ValorNormalizado != e.Etiqueta.ValorNormalizado)
            .Append(new ValorCategoria(e.Etiqueta.Valor, e.Etiqueta.ValorNormalizado))
            .ToList();

        return vista with { Categoria = e.Etiqueta.Categoria, Valores = valores };
    }
}
