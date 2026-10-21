using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.ModificarVentanaDeEntradaFunction;

public record ModificarVentanaDeEntrada(Guid TurnoId, HorasYMinutosSolicitadas Amplitud)
{
    public HorasYMinutos ToAmplitud() => throw new NotImplementedException();
}
