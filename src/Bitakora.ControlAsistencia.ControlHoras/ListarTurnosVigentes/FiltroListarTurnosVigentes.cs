namespace Bitakora.ControlAsistencia.ControlHoras.ListarTurnosVigentes;

/// <summary>
/// Filtro tipado del body (QUERY, MEF-ADR-0042, issue #440). DesdeFecha/HastaFecha son
/// obligatorias pese a declararse nullable -- nullable distingue "el campo no vino" (422 con
/// mensaje propio) de "el campo vino con un valor invalido para su tipo" (400 del catch de
/// JsonException, antes de llegar a este record). A diferencia del filtro homonimo de
/// ListarAsistenciasDiarias, CodigoColaborador y SedeId son OPCIONALES: su ausencia es el
/// panorama del Programador (regresion #329) y la ausencia de filtro por sede (regresion #337),
/// no un 422.
/// </summary>
public sealed record FiltroListarTurnosVigentes(
    DateOnly? DesdeFecha,
    DateOnly? HastaFecha,
    string? CodigoColaborador,
    string? SedeId);
