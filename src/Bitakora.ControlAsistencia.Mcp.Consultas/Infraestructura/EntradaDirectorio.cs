namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

/// <summary>
/// Contrato upstream de QUERY colaboradores/directorio, redeclarado aqui (cero referencias a los
/// ensamblados del BC, CA-ADR-0029). Solo los campos que la tool consume: TipoDocumento,
/// NumeroDocumento y TokensNombre ni siquiera se declaran -- STJ ignora lo que sobra en el JSON.
/// VigenteHasta llega null cuando la vinculacion esta abierta.
/// </summary>
public sealed record EntradaDirectorio(
    string Identificacion,
    string NombreCompleto,
    string CodigoColaborador,
    string? CodigoSede,
    DateOnly VigenteDesde,
    DateOnly? VigenteHasta);
