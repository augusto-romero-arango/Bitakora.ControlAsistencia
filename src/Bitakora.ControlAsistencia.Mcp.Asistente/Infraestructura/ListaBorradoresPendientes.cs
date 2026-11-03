namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

/// <summary>
/// Contrato upstream de QUERY programacion/borradores, redeclarado como isla
/// (MEF-ADR-0047 decision 3): solo los campos que la malla consume.
/// </summary>
public sealed record ListaBorradoresPendientes(
    IReadOnlyList<BorradorPendienteColaborador> Elementos,
    string? SiguienteCursor);

public sealed record BorradorPendienteColaborador(
    string CodigoColaborador,
    IReadOnlyList<DiaPendienteBorrador> Dias);

public sealed record DiaPendienteBorrador(
    DateOnly Fecha,
    string? NombreTurno,
    string? HorarioResumido,
    IReadOnlyList<BloqueMallaSemanal> Bloques,
    bool EsHorarioAbierto,
    bool Vencido);
