namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

/// <summary>
/// Sobre unico de lista de los endpoints del BC (CA-ADR-0039), redeclarado aqui (cero referencias
/// a los ensamblados del BC). Estas lecturas llaman sin take: el sobre llega sin siguienteCursor.
/// </summary>
public sealed record SobreDeLista<T>(IReadOnlyList<T> Elementos, string? SiguienteCursor = null);
