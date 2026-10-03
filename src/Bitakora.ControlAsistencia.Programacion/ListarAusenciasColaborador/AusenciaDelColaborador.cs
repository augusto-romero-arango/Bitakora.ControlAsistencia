using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.ListarAusenciasColaborador;

public sealed record TramoVigente(DateOnly Desde, DateOnly Hasta);

public sealed record AusenciaDelColaborador(
    Guid Id,
    MotivoAusencia Motivo,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    IReadOnlyList<TramoVigente> TramosVigentes)
{
    public bool Equals(AusenciaDelColaborador? other)
        => other is not null
           && Id == other.Id && Motivo == other.Motivo
           && FechaInicio == other.FechaInicio && FechaFin == other.FechaFin
           && TramosVigentes.SequenceEqual(other.TramosVigentes);

    public override int GetHashCode() => HashCode.Combine(Id, Motivo, FechaInicio, FechaFin, TramosVigentes.Count);
}
