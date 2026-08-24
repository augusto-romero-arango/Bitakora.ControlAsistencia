namespace Bitakora.ControlAsistencia.ControlHoras.ListarResumenesAsistencia;

/// <summary>
/// Filtro tipado del body (QUERY, MEF-ADR-0042), campos combinados con AND.
///
/// DesdeFecha/HastaFecha son obligatorias pese a declararse nullable: el nullable es lo que
/// distingue "el campo no vino" (422 con mensaje propio) de "vino con un valor invalido para su
/// tipo" (400 del catch de JsonException, antes de llegar a este record). Quitarles el '?' colapsa
/// el primer caso en el default de DateOnly y el 422 deja de emitirse.
///
/// Cursor keyset de un solo campo -- el CodigoColaborador de la ultima fila recibida --, sin
/// desempate: el codigo es unico por fila del resumen.
/// </summary>
public sealed record FiltroListarResumenesAsistencia(
    DateOnly? DesdeFecha,
    DateOnly? HastaFecha,
    IReadOnlyList<string>? CodigosColaborador,
    string? Cursor,
    int Take = 50);
