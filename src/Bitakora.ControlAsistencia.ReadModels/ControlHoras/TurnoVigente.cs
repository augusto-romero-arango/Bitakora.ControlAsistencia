namespace Bitakora.ControlAsistencia.ReadModels.ControlHoras;

/// <summary>
/// Turno que rige a un colaborador en una fecha.
///
/// Id es el stream key de ControlDiarioAggregateRoot.ComputarStreamId
/// ("cd:{CodigoColaborador}:{Fecha:yyyyMMdd}"), nunca un Guid. Excluye a proposito la trazabilidad
/// hacia la solicitud de programacion y la identificacion completa: ningun cliente las consume.
/// </summary>
[MaterializadoPorProyeccion]
public sealed record TurnoVigente(
    string Id,
    string CodigoColaborador,
    string NombreCompleto,
    DateOnly Fecha,
    string NombreTurno,
    string HorarioResumido,
    IReadOnlyList<Bloque> Bloques,
    string? MotivoAusencia = null,
    Guid? AusenciaId = null,
    TurnoCubierto? TurnoCubierto = null,
    bool EsHorarioAbierto = false);

/// <summary>
/// Turno que una ausencia vigente cubre: estado interno del documento para restablecerlo al
/// cancelar la ausencia. La respuesta de los endpoints no lo expone.
/// </summary>
public sealed record TurnoCubierto(
    string NombreTurno,
    string HorarioResumido,
    IReadOnlyList<Bloque> Bloques,
    bool EsHorarioAbierto = false)
{
    // Igualdad estructural: el equals sintetizado del record compara la lista por referencia.
    public bool Equals(TurnoCubierto? other) =>
        other is not null
        && NombreTurno == other.NombreTurno
        && HorarioResumido == other.HorarioResumido
        && Bloques.SequenceEqual(other.Bloques)
        && EsHorarioAbierto == other.EsHorarioAbierto;

    public override int GetHashCode() => HashCode.Combine(NombreTurno, HorarioResumido, Bloques.Count, EsHorarioAbierto);
}
