namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

/// <summary>
/// Contrato upstream de GET/QUERY colaboradores/fichas, redeclarado aqui (cero referencias al BC,
/// CA-ADR-0029) con solo los campos que la tool consume. VigenteHasta null = vinculacion abierta.
/// CodigoSede es el codigo tal cual, sin nombre resuelto.
/// </summary>
public sealed record FichaColaborador(
    string Id,
    string NombreCompleto,
    string CodigoColaborador,
    DateOnly VigenteDesde,
    DateOnly? VigenteHasta,
    IReadOnlyList<EtiquetaFicha> Etiquetas,
    string? CodigoSede,
    Guid? JornadaId = null);

/// <summary>Etiqueta en su forma original de presentacion (sin normalizar).</summary>
public sealed record EtiquetaFicha(string Categoria, string Valor);
