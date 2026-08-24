namespace Bitakora.ControlAsistencia.ControlHoras.ListarResumenesAsistencia;

/// <summary>
/// Filtro tipado del body (QUERY, MEF-ADR-0042), campos combinados con AND. DesdeFecha/HastaFecha
/// son obligatorias pese a declararse nullable -- nullable distingue "el campo no vino" (422 con
/// mensaje propio) de "el campo vino con un valor invalido para su tipo" (400 del catch de
/// JsonException, antes de llegar a este record). CodigosColaborador es el patron de recorte de
/// poblacion fijado en sesion 2026-08-24 (issue #428): sin el, el universo es "colaboradores con
/// &gt;= 1 fila en el rango"; con el, una fila por codigo pedido (incluida la sintetica).
///
/// Cursor keyset de un solo campo: el CodigoColaborador de la ultima fila recibida (el codigo es
/// unico por fila del resumen, sin desempate -- a diferencia del cursor compuesto de
/// ListarFichasColaborador). Take/TakeMaximo, mismo patron heredado de #373.
/// </summary>
public sealed record FiltroListarResumenesAsistencia(
    DateOnly? DesdeFecha,
    DateOnly? HastaFecha,
    IReadOnlyList<string>? CodigosColaborador,
    string? Cursor,
    int Take = 50);
