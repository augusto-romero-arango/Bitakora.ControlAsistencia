namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

/// <summary>Contrato upstream de GET sedes/fichas, redeclarado aqui (cero referencias al BC).</summary>
public sealed record FichaSede(
    string Id,
    string Codigo,
    string Nombre,
    string? Ciudad,
    string? Direccion,
    string? CentroDeCostos,
    bool Activa,
    IReadOnlyList<string> Dispositivos);
