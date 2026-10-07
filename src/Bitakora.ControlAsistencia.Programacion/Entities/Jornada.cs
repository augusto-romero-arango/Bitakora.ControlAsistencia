using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.ObtenerJornada;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

public partial class Jornada : AggregateRoot
{
    private LimitesJornada _limites = null!;

    public void Apply(JornadaCreada evento)
    {
        Id = evento.JornadaId.ToString();
        _limites = evento.Limites;
    }

    internal JornadaRespuesta Describir() => new(
        Guid.Parse(Id),
        Convertir(_limites.HorasSemanales),
        Convertir(_limites.TopeDiario),
        Convertir(_limites.MinimoDiario),
        _limites.DiasDescansoPorSemana,
        _limites.ToString());

    private static HorasYMinutosRespuesta Convertir(HorasYMinutos valor) => new(valor.Horas, valor.Minutos);

    internal static Jornada Iniciar(JornadaCreada evento)
    {
        var jornada = new Jornada();
        jornada._uncommittedEvents.Add(evento);
        jornada.Apply(evento);
        return jornada;
    }
}
