using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.AsignarModalidadAJornadaFunction;

public record AsignarModalidadAJornada(Guid JornadaId, string Modalidad)
{
    public ModalidadJornada ToModalidadJornada() => ModalidadJornada.Desde(Modalidad);
}
