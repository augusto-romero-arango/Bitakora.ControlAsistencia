using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.CrearTurnoDeHorarioAbiertoFunction;

public record CrearTurnoDeHorarioAbierto(Guid TurnoId, string Nombre, HorasYMinutosSolicitadas Duracion)
{
    public HorasYMinutos ToDuracion() => HorasYMinutos.Crear(Duracion.Horas, Duracion.Minutos);
}
