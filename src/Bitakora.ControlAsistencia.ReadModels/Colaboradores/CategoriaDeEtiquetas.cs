namespace Bitakora.ControlAsistencia.ReadModels.Colaboradores;

/// <summary>
/// Valor en uso dentro de una categoria de etiquetas -- forma ORIGINAL (display) y NORMALIZADA
/// (matcheo del autocompletado).
/// </summary>
/// <remarks>
/// Issue #357: tipo propio de ReadModels, SIN relacion de tipo con el VO Etiqueta de
/// Colaboradores.DomainEvents (islas, CA-ADR-0029; precedente EtiquetaFicha de #356). Record plano,
/// sin comportamiento: el upsert por valor normalizado vive en CategoriaDeEtiquetasProjection
/// (worker), nunca aqui.
/// </remarks>
public sealed record ValorCategoria(string Valor, string ValorNormalizado);

/// <summary>
/// Read model del catalogo de categorias de etiquetas en uso (issue #357) -- alimenta el
/// autocompletado de la UI de etiquetado/filtrado: el usuario elige una categoria/valor existente
/// en vez de teclear una variante que fragmentaria el reporte (complementa la normalizacion del VO
/// Etiqueta, #353). Consumido por ListarCategoriasDeEtiquetas (GET, opcion B: catalogo entero de un
/// tiro, sin filtros ni paginacion en esta primera version).
/// </summary>
/// <remarks>
/// Record plano SIN partial (MEF-ADR-0035, skills/projections/modelos-marten.md): el comportamiento
/// de proyeccion vive en la clase companion CategoriaDeEtiquetasProjection, en el worker
/// (Bitakora.ControlAsistencia.Projections). Este tipo vive en ReadModels (MEF-ADR-0034 seccion 5),
/// la cuarta isla del repo: cero referencias de proyecto.
///
/// Sin sufijo "View" (MEF-ADR-0041 decision 3).
///
/// Id es la categoria NORMALIZADA (Etiqueta.CategoriaNormalizada, #353) -- la identidad de
/// correlacion N2 (MultiStreamProjection&lt;CategoriaDeEtiquetas, string&gt;,
/// Identity&lt;EtiquetaAsignada&gt;(e =&gt; e.Etiqueta.CategoriaNormalizada)): eventos de MUCHOS
/// streams de ColaboradorAggregateRoot convergen en el MISMO documento cuando comparten categoria
/// normalizada, sin importar la forma original con la que cada colaborador la escribio.
///
/// Categoria y cada ValorCategoria.Valor reflejan la ULTIMA asignacion que toco esa categoria/valor
/// (decision de refinamiento 2026-08-12) -- "la ultima gana", mismo espiritu que la sobrescritura de
/// etiquetas en FichaColaborador (#356).
///
/// CATALOGO ACUMULATIVO (decision de refinamiento 2026-08-13): solo EtiquetaAsignada alimenta esta
/// vista -- EtiquetaRetirada NO descuenta ningun valor (ver CategoriaDeEtiquetasProjection, que no
/// declara ningun metodo para ese evento). Sobrescribir el valor de una categoria AGREGA el valor
/// nuevo a Valores y conserva el anterior; no hay borrado implicito, ni por sobrescritura ni por
/// reingreso-nace-limpio (ambos exigirian estado por colaborador, territorio N3 sin beneficio hoy
/// para el proposito del autocompletado -- Rule of Three, MEF-ADR-0018).
/// </remarks>
public sealed record CategoriaDeEtiquetas(
    string Id,
    string Categoria,
    IReadOnlyList<ValorCategoria> Valores);
