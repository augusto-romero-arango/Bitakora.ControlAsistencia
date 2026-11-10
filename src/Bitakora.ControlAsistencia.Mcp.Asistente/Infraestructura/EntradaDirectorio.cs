namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

/// <summary>
/// Contrato upstream de QUERY colaboradores/directorio, redeclarado aqui (cero referencias al BC,
/// CA-ADR-0029) con solo los campos que la tool consume. VigenteHasta null = vinculacion abierta.
/// </summary>
public sealed record EntradaDirectorio(
    string Identificacion,
    string NombreCompleto,
    string CodigoColaborador,
    string? CodigoSede,
    DateOnly VigenteDesde,
    DateOnly? VigenteHasta,
    Guid? JornadaId = null);
