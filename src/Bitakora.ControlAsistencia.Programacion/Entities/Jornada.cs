using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.ObtenerJornada;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

public partial class Jornada : AggregateRoot
{
    private Guid _jornadaId;
    private LimitesJornada _limites = null!;

    public void Apply(JornadaCreada evento)
    {
        _jornadaId = evento.JornadaId;
        Id = _jornadaId.ToString();
        _limites = evento.Limites;
    }

    public void Apply(LimitesJornadaModificados evento) => _limites = evento.Limites;

    internal ResultadoModificarLimites ModificarLimites(LimitesJornada limites)
    {
        if (_limites.Equals(limites))
            return ResultadoModificarLimites.SinCambios;

        var evento = LimitesJornadaModificados.Crear(_jornadaId, limites);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return ResultadoModificarLimites.Modificados;
    }

    internal JornadaRespuesta Describir() => new(
        _jornadaId,
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
