namespace Bitakora.ControlAsistencia.ReadModels.Colaboradores;

/// <summary>
/// Etiqueta dinamica (categoria:valor) tal como se muestra en la ficha -- forma ORIGINAL para
/// display, sin normalizar.
/// </summary>
/// <remarks>
/// Issue #356: tipo propio de ReadModels, SIN relacion de tipo con el VO Etiqueta de
/// Colaboradores.DomainEvents (islas, CA-ADR-0029; precedente Bloque/TipoBloque de #328). Record
/// plano, sin comportamiento: la normalizacion y el upsert por categoria viven en
/// FichaColaboradorProjection (worker), nunca aqui.
/// </remarks>
public sealed record EtiquetaFicha(string Categoria, string Valor);

/// <summary>
/// Read model de la ficha de un colaborador (issue #356) -- consulta puntual por identificacion,
/// base del flujo de reingreso (los adaptadores la consultan antes de decidir registrar-vs-
/// reingresar, por eso INCLUYE colaboradores no-vigentes). Primera vista materializada del dominio
/// Colaboradores.
/// </summary>
/// <remarks>
/// Record plano SIN partial (MEF-ADR-0035, skills/projections/modelos-marten.md): el comportamiento
/// de proyeccion vive en la clase companion FichaColaboradorProjection, en el worker
/// (Bitakora.ControlAsistencia.Projections). Este tipo vive en ReadModels (MEF-ADR-0034 seccion 5),
/// la cuarta isla del repo: cero referencias de proyecto (ver el .csproj) -- EtiquetaFicha es
/// propio, sin relacion de tipo con Colaboradores.DomainEvents.
///
/// Sin sufijo "View" (MEF-ADR-0041 decision 3, extension del issue #317 CA-2 al read-side,
/// precedente TurnoVigente #328).
///
/// Id es el stream key que compone ColaboradorAggregateRoot.ComputarStreamId(Identificacion):
/// "{Tipo}:{Numero}" (ej. "CC:123456") -- contrato de Identificacion.ToString(), nunca aplanado en
/// Tipo/Numero separados (decision de refinamiento 2026-08-12: redundantes con el Id).
///
/// CodigoColaborador, VigenteDesde y VigenteHasta reflejan siempre la ULTIMA vinculacion -- la
/// ficha NO acumula historial de vinculaciones (el historial completo vive en el stream). Un
/// reingreso (VinculacionIniciada tras terminacion) sobrescribe estos tres campos y VACIA ambas
/// estructuras de etiquetas ("reingreso nace limpio", espejo de la regla que #355 fijo en el
/// aggregate para Apply(VinculacionIniciada)).
///
/// VigenteHasta usa el CENTINELA 9999-12-31 cuando la vinculacion esta abierta -- estructura
/// INTERNA de filtrado/indexacion (el issue hermano de listado filtra "VigenteHasta >= fecha" como
/// una sola operacion de rango): jamas sale por la API (ObtenerFichaColaborador lo traduce a vacio,
/// CA-6; el mecanismo de esa traduccion es decision del implementer). Sin booleano de estado
/// materializado: el preaviso voltea de vigente a no-vigente sin evento de reloj, la vigencia se
/// evalua en la consulta, no aqui.
///
/// EtiquetasNormalizadas es la estructura de filtrado por containment JSONB que consumira el issue
/// hermano de listado; Etiquetas son las formas ORIGINALES para presentacion. Ambas se mantienen en
/// paralelo (upsert/retiro por categoria normalizada, un valor por categoria -- espejo de la
/// invariante que #355 fijo en el aggregate).
/// </remarks>
public sealed record FichaColaborador(
    string Id,
    string NombreCompleto,
    string CodigoColaborador,
    DateOnly VigenteDesde,
    DateOnly VigenteHasta,
    IReadOnlyList<EtiquetaFicha> Etiquetas,
    IReadOnlyDictionary<string, string> EtiquetasNormalizadas);
