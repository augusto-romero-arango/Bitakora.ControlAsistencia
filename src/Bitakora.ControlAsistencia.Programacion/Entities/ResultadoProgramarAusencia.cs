using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

internal abstract record ResultadoProgramarAusencia
{
    internal sealed record Programada : ResultadoProgramarAusencia;

    internal sealed record ChocaConAusencia(
        IReadOnlyList<DateOnly> FechasEnConflicto, MotivoAusencia Motivo) : ResultadoProgramarAusencia;

    internal sealed record IdDuplicado : ResultadoProgramarAusencia;
}
