namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

/// <summary>
/// Sobre de lista de los endpoints del BC (CA-ADR-0039), redeclarado aqui. Las composiciones internas
/// llaman sin take y el sobre llega sin siguienteCursor (CA-ADR-0038).
/// </summary>
public sealed record SobreDeLista<T>(IReadOnlyList<T> Elementos, string? SiguienteCursor = null);
