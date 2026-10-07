using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.ModificarLimitesJornadaFunction;

public record ModificarLimitesJornada(Guid JornadaId, HorasYMinutosSolicitadas HorasSemanales,
    HorasYMinutosSolicitadas TopeDiario, HorasYMinutosSolicitadas MinimoDiario,
    int DiasDescansoPorSemana)
{
    public LimitesJornada ToLimitesJornada() => throw new NotImplementedException();
}
