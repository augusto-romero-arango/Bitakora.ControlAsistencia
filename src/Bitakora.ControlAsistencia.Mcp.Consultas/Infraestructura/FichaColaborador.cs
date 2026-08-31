namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

/// <summary>
/// Contrato upstream de GET/QUERY colaboradores/fichas, redeclarado aqui (cero referencias a los
/// ensamblados del BC, CA-ADR-0029). Solo los campos que la tool consume: EtiquetasNormalizadas
/// (estructura interna de filtrado del read model) ni siquiera se declara -- STJ ignora lo que
/// sobra en el JSON. VigenteHasta llega null cuando la vinculacion esta abierta: el backend ya
/// oculta el centinela de vigencia (issue #356 CA-6). CodigoSede (issue #519) es el codigo tal
/// cual, sin nombre resuelto.
/// </summary>
public sealed record FichaColaborador(
    string Id,
    string NombreCompleto,
    string CodigoColaborador,
    DateOnly VigenteDesde,
    DateOnly? VigenteHasta,
    IReadOnlyList<EtiquetaFicha> Etiquetas,
    string? CodigoSede);

/// <summary>Etiqueta en su forma original de presentacion (sin normalizar).</summary>
public sealed record EtiquetaFicha(string Categoria, string Valor);
