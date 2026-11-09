namespace Bitakora.ControlAsistencia.PrivateEvents.Programacion;

public record DetalleJornada(
    Guid JornadaId,
    int HorasSemanalesEnMinutos,
    int TopeDiarioEnMinutos,
    int MinimoDiarioEnMinutos,
    int DiasDescansoPorSemana,
    string? Modalidad = null,
    bool DescansoAcumulado = false,
    int? TopeDiarioDeLeyEnMinutos = null,
    int? MinimoDiarioDeLeyEnMinutos = null,
    int? HorasSemanalesDeLeyEnMinutos = null);
