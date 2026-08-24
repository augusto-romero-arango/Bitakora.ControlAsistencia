namespace Bitakora.ControlAsistencia.ControlHoras.ListarAsistenciasDiarias;

/// <summary>
/// Filtro tipado del body (QUERY, MEF-ADR-0042), campos combinados con AND. Los tres son
/// obligatorios pese a declararse nullable: nullable distingue "el campo no vino" (422 con mensaje
/// propio) de "el campo vino con un valor invalido para su tipo" (400 del catch de JsonException,
/// antes de llegar a este record).
/// </summary>
public sealed record FiltroListarAsistenciasDiarias(
    string? CodigoColaborador,
    DateOnly? DesdeFecha,
    DateOnly? HastaFecha);
