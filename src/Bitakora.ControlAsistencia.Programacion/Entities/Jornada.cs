using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

public partial class Jornada : AggregateRoot
{
    public void Apply(JornadaCreada evento) => throw new NotImplementedException();
    internal static Jornada Iniciar(JornadaCreada evento) => throw new NotImplementedException();
}
