namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

/// <summary>
/// Contrato upstream de GET/QUERY colaboradores/fichas, redeclarado aqui (cero referencias a los
/// ensamblados del BC, CA-ADR-0029). VigenteHasta llega null cuando la vinculacion esta abierta:
/// el backend ya oculta el centinela de vigencia (issue #356 CA-6), asi que este cliente nunca lo
/// ve. CodigoSede (issue #519) es el codigo tal cual, sin nombre resuelto.
/// </summary>
public sealed record FichaColaborador(
    string Id,
    string NombreCompleto,
    string CodigoColaborador,
    DateOnly VigenteDesde,
    DateOnly? VigenteHasta,
    IReadOnlyList<EtiquetaFicha> Etiquetas,
    IReadOnlyDictionary<string, string> EtiquetasNormalizadas,
    string? CodigoSede);

/// <summary>Etiqueta en su forma original de presentacion (sin normalizar).</summary>
public sealed record EtiquetaFicha(string Categoria, string Valor);
