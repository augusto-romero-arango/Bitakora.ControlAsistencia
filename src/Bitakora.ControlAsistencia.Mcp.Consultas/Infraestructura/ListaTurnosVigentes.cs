namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

/// <summary>
/// Contrato upstream de QUERY control-horas/turnos-vigentes, redeclarado aqui (cero referencias a
/// los ensamblados del BC, CA-1 del issue #502). NombreCompleto es anulable aunque el read model
/// upstream lo declare string: los datos de dev traen null en turnos anteriores al campo.
/// </summary>
public sealed record ListaTurnosVigentes(
    DateOnly DesdeAplicado,
    DateOnly HastaAplicado,
    bool RangoRecortado,
    IReadOnlyList<TurnoVigente> Turnos);

public sealed record TurnoVigente(
    string Id,
    string CodigoColaborador,
    string? NombreCompleto,
    DateOnly Fecha,
    string NombreTurno,
    string HorarioResumido,
    IReadOnlyList<BloqueVigente> Bloques);

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
