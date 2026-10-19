namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

/// <summary>
/// Sobre unico de lista de los endpoints del BC (CA-ADR-0039), redeclarado aqui (cero referencias
/// a los ensamblados del BC). Las composiciones internas llaman sin take y el sobre llega sin
/// siguienteCursor (CA-ADR-0038); los catalogos piden take y lo reenvian.
/// </summary>
public sealed record SobreDeLista<T>(IReadOnlyList<T> Elementos, string? SiguienteCursor = null);
