namespace Bitakora.ControlAsistencia.ControlHoras.ListarTurnosVigentes;

/// <summary>
/// Filtro tipado del body (QUERY, MEF-ADR-0042), campos combinados con AND. DesdeFecha/HastaFecha
/// son obligatorias pese a declararse nullable: nullable distingue "el campo no vino" (422 con
/// mensaje propio) de "el campo vino con un valor invalido para su tipo" (400 del catch de
/// JsonException, antes de llegar a este record). CodigoColaborador y SedeId, en cambio, son
/// opcionales de verdad -- su ausencia es el panorama de todos los colaboradores y la ausencia de
/// filtro por sede, nunca un 422.
/// </summary>
public sealed record FiltroListarTurnosVigentes(
    DateOnly? DesdeFecha,
    DateOnly? HastaFecha,
    string? CodigoColaborador,
    string? SedeId);
