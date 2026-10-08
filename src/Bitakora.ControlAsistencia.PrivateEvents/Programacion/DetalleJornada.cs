namespace Bitakora.ControlAsistencia.PrivateEvents.Programacion;

public record DetalleJornada(
    Guid JornadaId,
    int HorasSemanalesEnMinutos,
    int TopeDiarioEnMinutos,
    int MinimoDiarioEnMinutos,
    int DiasDescansoPorSemana);
