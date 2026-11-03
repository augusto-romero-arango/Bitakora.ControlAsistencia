namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public sealed record BorradorDeProgramacion(
    string CodigoColaborador, string Identificacion, string NombreCompleto, IReadOnlyList<DiaDeBorrador> Dias);

/// <summary>Misma forma de casilla que la malla semanal, para superponerla sin traducir.</summary>
public sealed record DiaDeBorrador(
    DateOnly Fecha,
    string? NombreTurno,
    string? HorarioResumido,
    IReadOnlyList<BloqueMallaSemanal> Bloques,
    bool EsHorarioAbierto,
    bool Vencido);
