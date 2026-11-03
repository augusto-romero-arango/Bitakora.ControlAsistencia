namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public sealed record BorradorDeProgramacion(
    string CodigoColaborador, string NombreCompleto, IReadOnlyList<DiaDeBorrador> Dias);

public sealed record DiaDeBorrador(DateOnly Fecha);
