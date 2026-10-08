using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.ObtenerJornada;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

public partial class Jornada : AggregateRoot
{
    private const int MinutosPorHora = 60;
    private Guid _jornadaId;
    private LimitesJornada _limites = null!;
    private long _version;

    public void Apply(JornadaCreada evento)
    {
        _version++;
        _jornadaId = evento.JornadaId;
        Id = _jornadaId.ToString();
        _limites = evento.Limites;
    }

    public void Apply(LimitesJornadaModificados evento)
    {
        _version++;
        _limites = evento.Limites;
    }

    internal LimitesJornada Limites => _limites;

    internal ResultadoModificarLimites ModificarLimites(LimitesJornada limites)
    {
        if (_limites.Equals(limites))
            return ResultadoModificarLimites.SinCambios;

        var evento = LimitesJornadaModificados.Crear(_jornadaId, limites);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return ResultadoModificarLimites.Modificados;
    }

    internal JornadaRespuesta Describir(Guid? predeterminadaId = null) => new(
        _jornadaId,
        Convertir(_limites.HorasSemanales),
        Convertir(_limites.TopeDiario),
        Convertir(_limites.MinimoDiario),
        _limites.DiasDescansoPorSemana,
        _limites.ToString(),
        predeterminadaId == _jornadaId);

    private static HorasYMinutosRespuesta Convertir(HorasYMinutos valor) => new(valor.Horas, valor.Minutos);

    private static int EnMinutos(HorasYMinutos valor) => valor.Horas * MinutosPorHora + valor.Minutos;

    internal LimitesDeJornada ComoVista() => new(
        _jornadaId.ToString(),
        EnMinutos(_limites.HorasSemanales),
        EnMinutos(_limites.TopeDiario),
        EnMinutos(_limites.MinimoDiario),
        _limites.DiasDescansoPorSemana,
        _limites.ToString());

    internal LimitesDeJornadaActualizados ComoLimitesActualizados() => new(
        _jornadaId,
        _version,
        EnMinutos(_limites.HorasSemanales),
        EnMinutos(_limites.TopeDiario),
        EnMinutos(_limites.MinimoDiario),
        _limites.DiasDescansoPorSemana);

    internal JornadaProgramada Estampar() => new(_jornadaId, _limites);

    internal static Jornada Iniciar(JornadaCreada evento)
    {
        var jornada = new Jornada();
        jornada._uncommittedEvents.Add(evento);
        jornada.Apply(evento);
        return jornada;
    }
}
