namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

/// <summary>
/// Contrato upstream de QUERY control-horas/turnos-vigentes, redeclarado aqui (cero referencias al BC).
/// NombreCompleto es anulable aunque upstream lo declare string: los datos de dev traen null en
/// turnos anteriores al campo.
/// </summary>
public sealed record ListaTurnosVigentes(
    DateOnly Desde,
    DateOnly Hasta,
    bool RangoRecortado,
    IReadOnlyList<TurnoVigente> Elementos,
    string? SiguienteCursor = null);

public sealed record TurnoVigente(
    string Id,
    string CodigoColaborador,
    string? NombreCompleto,
    DateOnly Fecha,
    string NombreTurno,
    string HorarioResumido,
    IReadOnlyList<BloqueVigente> Bloques,
    bool EsHorarioAbierto = false);

/// <summary>Bloque del turno vigente; Tipo llega como numero en el JSON upstream.</summary>
public sealed record BloqueVigente(
    TipoBloque Tipo,
    DateTime Inicio,
    DateTime Fin,
    string? SedeId,
    string? NombreSede);

public enum TipoBloque
{
    Ordinaria,
    Descanso,
    Extra
}
