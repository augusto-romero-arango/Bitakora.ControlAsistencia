using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

public partial class Jornada : AggregateRoot
{
    public void Apply(JornadaCreada evento) => Id = evento.JornadaId.ToString();

    internal static Jornada Iniciar(JornadaCreada evento)
    {
        var jornada = new Jornada();
        jornada._uncommittedEvents.Add(evento);
        jornada.Apply(evento);
        return jornada;
    }
}
