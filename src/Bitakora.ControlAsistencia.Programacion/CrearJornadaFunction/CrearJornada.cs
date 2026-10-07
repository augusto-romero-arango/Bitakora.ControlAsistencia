namespace Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;

public record HorasYMinutosSolicitadas(int Horas, int Minutos);

public record CrearJornada(Guid JornadaId, HorasYMinutosSolicitadas HorasSemanales,
    HorasYMinutosSolicitadas TopeDiario, HorasYMinutosSolicitadas MinimoDiario,
    int DiasDescansoPorSemana);
