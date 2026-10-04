namespace Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;

public sealed record ResultadoCandidatos(IReadOnlyList<CandidatoProgramacion> Candidatos, string? FalloDeLectura);
