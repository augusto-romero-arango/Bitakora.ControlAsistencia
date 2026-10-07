using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;

namespace Bitakora.ControlAsistencia.Programacion.ModificarLimitesJornadaFunction;

public record ModificarLimitesJornadaBody(HorasYMinutosSolicitadas HorasSemanales,
    HorasYMinutosSolicitadas TopeDiario, HorasYMinutosSolicitadas MinimoDiario,
    int DiasDescansoPorSemana);
