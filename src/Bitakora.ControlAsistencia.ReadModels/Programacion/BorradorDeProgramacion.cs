namespace Bitakora.ControlAsistencia.ReadModels.Programacion;

/// <summary>
/// El borrador de un colaborador: los dias programados que aun no se publican, con todos sus meses.
/// Id es el CodigoColaborador (N2: el documento cruza los streams mensuales bm:{codigo}:{yyyyMM}).
/// Vencido no se guarda: lo calcula quien consulta contra su FechaReferencia.
/// </summary>
[MaterializadoPorProyeccion]
public sealed record BorradorDeProgramacion(
    string Id,
    string CodigoColaborador,
    string Identificacion,
    string NombreCompleto,
    IReadOnlyList<DiaDeBorrador> Dias);

/// <summary>Casilla de un dia en borrador; misma forma que TurnoVigente (ControlHoras).</summary>
public sealed record DiaDeBorrador(
    DateOnly Fecha,
    string NombreTurno,
    string HorarioResumido,
    IReadOnlyList<Bloque> Bloques,
    bool EsHorarioAbierto = false);

public sealed record Bloque(
    TipoBloque Tipo,
    DateTime Inicio,
    DateTime Fin,
    string? SedeId = null,
    string? NombreSede = null);

public enum TipoBloque
{
    Ordinaria,
    Descanso,
    Extra
}
