using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.ListarAusenciasColaborador;

public sealed record TramoVigente(DateOnly Desde, DateOnly Hasta);

public sealed record AusenciaDelColaborador(
    Guid Id,
    MotivoAusencia Motivo,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    IReadOnlyList<TramoVigente> TramosVigentes);
