namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

/// <summary>
/// Contrato upstream de GET sedes/fichas, redeclarado aqui (cero referencias a los ensamblados
/// del BC, CA-1 del issue #502).
/// </summary>
public sealed record FichaSede(
    string Id,
    string Codigo,
    string Nombre,
    string? Ciudad,
    string? Direccion,
    string? CentroDeCostos,
    bool Activa,
    IReadOnlyList<string> Dispositivos);
