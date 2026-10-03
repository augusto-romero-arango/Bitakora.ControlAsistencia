using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

internal sealed record ClasificacionFechas(
    IReadOnlyList<DateOnly> Libres, IReadOnlyList<FechaConAusencia> ConAusencia);

internal sealed record FechaConAusencia(DateOnly Fecha, MotivoAusencia Motivo);
