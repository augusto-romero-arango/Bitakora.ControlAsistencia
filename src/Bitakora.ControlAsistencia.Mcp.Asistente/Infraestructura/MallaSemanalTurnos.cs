namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

/// <summary>
/// Contrato upstream de QUERY control-horas/malla-semanal-turnos, redeclarado como isla
/// (MEF-ADR-0047 decision 3): solo los campos que la tool consume.
/// </summary>
public sealed record ListaMallaSemanal(
    DateOnly Desde,
    DateOnly Hasta,
    IReadOnlyList<FilaMallaSemanal> Elementos,
    string? SiguienteCursor);

public sealed record FilaMallaSemanal(string CodigoColaborador, IReadOnlyList<CasillaMallaSemanal> Casillas);

public sealed record CasillaMallaSemanal(
    DateOnly Fecha,
    string Tipo,
    string? NombreTurno,
    string? HorarioResumido,
    IReadOnlyList<BloqueMallaSemanal> Bloques,
    string? MotivoAusencia);

public sealed record BloqueMallaSemanal(string? SedeId, string? NombreSede);

public sealed record ColaboradorVigenteMalla(string CodigoColaborador, DateOnly VigenteDesde, DateOnly? VigenteHasta);
