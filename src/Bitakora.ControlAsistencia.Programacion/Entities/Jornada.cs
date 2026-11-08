using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.ObtenerJornada;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

public partial class Jornada : AggregateRoot
{
    private Guid _jornadaId;
    private LimitesJornada _limites = null!;
    private long _version;
    private bool _activa;
    private ModalidadJornada? _modalidad;
    private bool _descansoAcumulado;

    public void Apply(JornadaCreada evento)
    {
        _version++;
        _jornadaId = evento.JornadaId;
        Id = _jornadaId.ToString();
        _limites = evento.Limites;
        _activa = true;
    }

    public void Apply(JornadaDesactivada evento) => _activa = false;

    public void Apply(JornadaActivada evento) => _activa = true;

    internal bool Activa => _activa;

    internal ResultadoActivarJornada Activar()
    {
        if (_activa)
            return ResultadoActivarJornada.YaActiva;

        var evento = new JornadaActivada(_jornadaId);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return ResultadoActivarJornada.Activada;
    }

    internal ResultadoDesactivarJornada Desactivar()
    {
        if (!_activa)
            return ResultadoDesactivarJornada.YaInactiva;

        var evento = new JornadaDesactivada(_jornadaId);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return ResultadoDesactivarJornada.Desactivada;
    }

    public void Apply(LimitesJornadaModificados evento)
    {
        _version++;
        _limites = evento.Limites;
    }

    public void Apply(ModalidadDeJornadaAsignada evento) => _modalidad = evento.Modalidad;

    public void Apply(ModalidadDeJornadaQuitada evento) => _modalidad = null;

    public void Apply(DescansoAcumuladoDeJornadaMarcado evento) => _descansoAcumulado = true;

    public void Apply(DescansoAcumuladoDeJornadaDesmarcado evento) => _descansoAcumulado = false;

    internal bool DescansoAcumulado => _descansoAcumulado;

    internal void MarcarDescansoAcumulado()
    {
        if (_descansoAcumulado)
            return;

        var evento = DescansoAcumuladoDeJornadaMarcado.Crear(_jornadaId);
        _uncommittedEvents.Add(evento);
        Apply(evento);
    }

    internal void DesmarcarDescansoAcumulado()
    {
        if (!_descansoAcumulado)
            return;

        var evento = DescansoAcumuladoDeJornadaDesmarcado.Crear(_jornadaId);
        _uncommittedEvents.Add(evento);
        Apply(evento);
    }

    internal JornadaDelCatalogo? BuscarColisionDeMarca(bool descansoAcumulado, IReadOnlyList<JornadaDelCatalogo> catalogo) =>
        _descansoAcumulado == descansoAcumulado
            ? null
            : catalogo.FirstOrDefault(j => j.JornadaId != _jornadaId && j.TieneIdentidad(_limites, _modalidad, descansoAcumulado));

    internal ModalidadJornada? Modalidad => _modalidad;

    internal JornadaDelCatalogo? BuscarColision(ModalidadJornada? modalidad, IReadOnlyList<JornadaDelCatalogo> catalogo) =>
        Equals(_modalidad, modalidad)
            ? null
            : catalogo.FirstOrDefault(j => j.JornadaId != _jornadaId && j.TieneIdentidad(_limites, modalidad, _descansoAcumulado));

    internal JornadaDelCatalogo? BuscarColisionDeLimites(LimitesJornada limites, IReadOnlyList<JornadaDelCatalogo> catalogo) =>
        catalogo.FirstOrDefault(j => j.JornadaId != _jornadaId && j.TieneIdentidad(limites, _modalidad, _descansoAcumulado));

    internal ResultadoAsignarModalidad AsignarModalidad(ModalidadJornada modalidad)
    {
        if (Equals(_modalidad, modalidad))
            return ResultadoAsignarModalidad.SinCambios;

        var evento = ModalidadDeJornadaAsignada.Crear(_jornadaId, modalidad);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return ResultadoAsignarModalidad.Asignada;
    }

    internal ResultadoQuitarModalidad QuitarModalidad()
    {
        if (_modalidad is null)
            return ResultadoQuitarModalidad.SinCambios;

        var evento = ModalidadDeJornadaQuitada.Crear(_jornadaId);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return ResultadoQuitarModalidad.Quitada;
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
        DescripcionJornada.Componer(_limites, _modalidad, _descansoAcumulado),
        predeterminadaId == _jornadaId,
        _activa,
        _modalidad?.Nombre,
        _descansoAcumulado,
        DescripcionesDeAdvertenciasDeJornada.Describir(_modalidad, _limites));

    private static HorasYMinutosRespuesta Convertir(HorasYMinutos valor) => new(valor.Horas, valor.Minutos);

    internal LimitesDeJornada ComoVista() => new(
        _jornadaId.ToString(),
        _limites.HorasSemanales.TotalMinutos(),
        _limites.TopeDiario.TotalMinutos(),
        _limites.MinimoDiario.TotalMinutos(),
        _limites.DiasDescansoPorSemana,
        DescripcionJornada.Componer(_limites, _modalidad, _descansoAcumulado),
        _activa,
        _modalidad?.Nombre,
        _descansoAcumulado);

    internal LimitesDeJornadaActualizados ComoLimitesActualizados() => new(
        _jornadaId,
        _version,
        _limites.HorasSemanales.TotalMinutos(),
        _limites.TopeDiario.TotalMinutos(),
        _limites.MinimoDiario.TotalMinutos(),
        _limites.DiasDescansoPorSemana);

    internal JornadaProgramada Estampar() => new(_jornadaId, _limites, _modalidad, _descansoAcumulado, _modalidad?.FichaDeLey());

    internal static Jornada Iniciar(JornadaCreada evento, ModalidadJornada? modalidad = null, bool descansoAcumulado = false)
    {
        var jornada = new Jornada();
        jornada._uncommittedEvents.Add(evento);
        jornada.Apply(evento);
        if (modalidad is not null)
            jornada.AsignarModalidad(modalidad);
        if (descansoAcumulado)
            jornada.MarcarDescansoAcumulado();
        return jornada;
    }
}
