namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

public record JornadaProgramada(
    Guid JornadaId,
    int HorasSemanalesEnMinutos,
    int TopeDiarioEnMinutos,
    int MinimoDiarioEnMinutos,
    int DiasDescansoPorSemana);
