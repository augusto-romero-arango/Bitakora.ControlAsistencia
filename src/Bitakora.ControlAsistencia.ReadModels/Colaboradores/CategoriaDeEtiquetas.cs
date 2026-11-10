namespace Bitakora.ControlAsistencia.ReadModels.Colaboradores;

/// <summary>
/// Valor en uso dentro de una categoria de etiquetas -- forma ORIGINAL (display) y NORMALIZADA
/// (matcheo del autocompletado).
/// </summary>
public sealed record ValorCategoria(string Valor, string ValorNormalizado);

/// <summary>
/// Catalogo de categorias de etiquetas en uso, para el autocompletado: el usuario elige una
/// categoria/valor existente en vez de teclear una variante que fragmentaria el reporte.
/// </summary>
/// <remarks>
/// Id es la categoria NORMALIZADA: las etiquetas de todos los colaboradores con la misma categoria
/// normalizada convergen en este documento, sin importar como la escribio cada uno.
///
/// Categoria y cada ValorCategoria.Valor reflejan la ULTIMA asignacion que los toco.
///
/// Catalogo acumulativo: EtiquetaRetirada no descuenta y sobrescribir una categoria agrega el valor
/// nuevo sin quitar el anterior. Borrar exigiria estado por colaborador, sin beneficio para el
/// autocompletado.
/// </remarks>
[MaterializadoPorProyeccion]
public sealed record CategoriaDeEtiquetas(
    string Id,
    string Categoria,
    IReadOnlyList<ValorCategoria> Valores);
