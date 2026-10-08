using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

// El diseno del turno vive en el VO Turno; el aggregate conserva el ciclo de vida (_estaActivo)
// y las decisiones, preguntandole al turno.
public class CatalogoTurnos : AggregateRoot
{
    private Turno _turno = Turno.Crear(string.Empty, false, []);
    private bool _estaActivo;

    // Version del stream tal como la ve el aggregate: un evento por Apply, tanto al rehidratar como
    // al emitir. No depende de AggregateRoot.Version, que el harness de tests no puebla.
    private long _eventosAplicados;

    internal Turno Turno => _turno;

    public void Apply(TurnoCreado evento)
    {
        _eventosAplicados++;
        Id = evento.TurnoId.ToString();
        _turno = Turno.Crear(evento.Nombre, evento.EsDescanso, evento.FranjasOrdinarias);
        _estaActivo = true;
    }

    // MEF-ADR-0004 capa 4: no lanza -- la guarda de "ya retirado" decide en Retirar(), antes de
    // emitir.
    public void Apply(TurnoRetirado evento)
    {
        _eventosAplicados++;
        _estaActivo = false;
    }

    // MEF-ADR-0004 capa 4: las transformaciones del Turno no lanzan ni invocan factories con
    // invariantes (ConDescanso/ConExtra); sobre un stream anomalo devuelven el turno igual, porque
    // un Apply que lanza deja el aggregate roto para siempre.
    public void Apply(FranjaAgregada evento)
    {
        _eventosAplicados++;
        _turno = _turno.ConFranja(evento.Franja);
    }

    public void Apply(DescansoAgregado evento)
    {
        _eventosAplicados++;
        _turno = _turno.ConFranjaReemplazada(evento.Franja);
    }

    public void Apply(ExtraAgregado evento)
    {
        _eventosAplicados++;
        _turno = _turno.ConFranjaReemplazada(evento.Franja);
    }

    public void Apply(FranjaQuitada evento)
    {
        _eventosAplicados++;
        _turno = _turno.SinFranjaQueEmpiezaA(evento.Franja);
    }

    public void Apply(DescansoQuitado evento)
    {
        _eventosAplicados++;
        _turno = _turno.ConFranjaReemplazada(evento.Franja);
    }

    public void Apply(ExtraQuitado evento)
    {
        _eventosAplicados++;
        _turno = _turno.ConFranjaReemplazada(evento.Franja);
    }

    public void Apply(SedeDeFranjaAsignada evento)
    {
        _eventosAplicados++;
        _turno = _turno.ConFranjaReemplazada(evento.Franja);
    }

    public void Apply(SedeDeFranjaRetirada evento)
    {
        _eventosAplicados++;
        _turno = _turno.ConFranjaReemplazada(evento.Franja);
    }

    // Mecanismo "declinar con resultado" (CA-ADR-0030): el aggregate nunca lanza -- retorna la
    // razon del rechazo y el handler la traduce al status code (409 Conflict).
    internal ResultadoRetiroTurno Retirar()
    {
        if (!_estaActivo)
            return ResultadoRetiroTurno.SinCambios;

        var evento = TurnoRetirado.Crear(Guid.Parse(Id!));
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return ResultadoRetiroTurno.Retirado;
    }

    // Recibe la franja ya construida: las invariantes del VO las resolvio el handler, para no
    // mezclar ese canal de error (lanzar) con el de las reglas de negocio (declinar, CA-ADR-0030).
    internal ResultadoAgregarFranja AgregarFranja(FranjaOrdinaria franja)
    {
        if (!_estaActivo)
            return ResultadoAgregarFranja.TurnoRetirado;

        if (_turno.EsDescanso())
            return ResultadoAgregarFranja.TurnoEsDescanso;

        if (_turno.SeSolapaCon(franja))
            return ResultadoAgregarFranja.SeSolapaConOtraFranja;

        var evento = FranjaAgregada.Crear(Guid.Parse(Id!), franja);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return ResultadoAgregarFranja.Agregada;
    }

    // Localiza la franja contenedora por hora de inicio (EmpiezaA), delega la construccion de la
    // hija en ConDescanso (invariantes del VO, #600) y declina con resultado (CA-ADR-0030): la
    // ArgumentException del VO sube sin capturarse -- es el unico canal de error mezclado que
    // acepta esta familia de comandos de diseno.
    internal ResultadoAgregarSubFranja AgregarDescanso(
        TimeOnly horaInicioFranja, TimeOnly inicio, TimeOnly fin)
    {
        var precondicion = EvaluarPrecondicionSubFranja();
        if (precondicion is not null)
            return precondicion.Value;

        var franjaActual = _turno.FranjaQueEmpiezaA(horaInicioFranja);
        if (franjaActual is null)
            return ResultadoAgregarSubFranja.FranjaNoExiste;

        var evento = DescansoAgregado.Crear(
            Guid.Parse(Id!), franjaActual.ConDescanso(inicio, fin));
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return ResultadoAgregarSubFranja.Agregada;
    }

    internal ResultadoAgregarSubFranja AgregarExtra(
        TimeOnly horaInicioFranja, TimeOnly inicio, TimeOnly fin)
    {
        var precondicion = EvaluarPrecondicionSubFranja();
        if (precondicion is not null)
            return precondicion.Value;

        var franjaActual = _turno.FranjaQueEmpiezaA(horaInicioFranja);
        if (franjaActual is null)
            return ResultadoAgregarSubFranja.FranjaNoExiste;

        var evento = ExtraAgregado.Crear(
            Guid.Parse(Id!), franjaActual.ConExtra(inicio, fin));
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return ResultadoAgregarSubFranja.Agregada;
    }

    // Precedencia: retirado > franja no existe. Un descanso no necesita resultado propio: no
    // tiene franjas ordinarias, asi que ya cae en FranjaNoExiste.
    internal ResultadoQuitarFranja QuitarFranja(TimeOnly horaInicio)
    {
        if (!_estaActivo)
            return ResultadoQuitarFranja.TurnoRetirado;

        var franjaActual = _turno.FranjaQueEmpiezaA(horaInicio);
        if (franjaActual is null)
            return ResultadoQuitarFranja.FranjaNoExiste;

        var evento = FranjaQuitada.Crear(Guid.Parse(Id!), franjaActual);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return ResultadoQuitarFranja.Quitada;
    }

    // Sin ArgumentException que mezclar con las reglas de negocio (CA-ADR-0030), a diferencia de
    // AgregarDescanso/AgregarExtra: quitar una hija nunca viola las invariantes del VO.
    internal ResultadoQuitarSubFranja QuitarDescanso(TimeOnly horaInicioFranja, TimeOnly horaInicioHija)
    {
        if (!_estaActivo)
            return ResultadoQuitarSubFranja.TurnoRetirado;

        var franjaActual = _turno.FranjaQueEmpiezaA(horaInicioFranja);
        if (franjaActual is null)
            return ResultadoQuitarSubFranja.FranjaNoExiste;

        var franjaResultante = franjaActual.SinDescanso(horaInicioHija);
        if (franjaResultante is null)
            return ResultadoQuitarSubFranja.SubFranjaNoExiste;

        var evento = DescansoQuitado.Crear(Guid.Parse(Id!), franjaResultante);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return ResultadoQuitarSubFranja.Quitada;
    }

    internal ResultadoQuitarSubFranja QuitarExtra(TimeOnly horaInicioFranja, TimeOnly horaInicioHija)
    {
        if (!_estaActivo)
            return ResultadoQuitarSubFranja.TurnoRetirado;

        var franjaActual = _turno.FranjaQueEmpiezaA(horaInicioFranja);
        if (franjaActual is null)
            return ResultadoQuitarSubFranja.FranjaNoExiste;

        var franjaResultante = franjaActual.SinExtra(horaInicioHija);
        if (franjaResultante is null)
            return ResultadoQuitarSubFranja.SubFranjaNoExiste;

        var evento = ExtraQuitado.Crear(Guid.Parse(Id!), franjaResultante);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return ResultadoQuitarSubFranja.Quitada;
    }

    // Mismo mecanismo "declinar con resultado" que QuitarFranja (CA-ADR-0030). Precedencia:
    // TurnoRetirado > FranjaNoExiste > FranjaSinSede. La sede incompleta NO se declina aqui: es
    // invariante del VO, y ConSede la deja subir como ArgumentException (400, no 409).
    internal ResultadoAsignarSedeAFranja AsignarSedeAFranja(TimeOnly horaInicioFranja, SedeProgramada? sede)
    {
        if (!_estaActivo)
            return ResultadoAsignarSedeAFranja.TurnoRetirado;

        var franja = _turno.FranjaQueEmpiezaA(horaInicioFranja);
        if (franja is null)
            return ResultadoAsignarSedeAFranja.FranjaNoExiste;

        if (sede is null)
        {
            if (!franja.TieneSedePrearmada())
                return ResultadoAsignarSedeAFranja.FranjaSinSede;

            var retiro = SedeDeFranjaRetirada.Crear(Guid.Parse(Id!), franja.ConSede(null));
            _uncommittedEvents.Add(retiro);
            Apply(retiro);
            return ResultadoAsignarSedeAFranja.Retirada;
        }

        var asignacion = SedeDeFranjaAsignada.Crear(Guid.Parse(Id!), franja.ConSede(sede));
        _uncommittedEvents.Add(asignacion);
        Apply(asignacion);
        return ResultadoAsignarSedeAFranja.Asignada;
    }

    // Precondiciones compartidas por AgregarDescanso/AgregarExtra (precedencia: retirado >
    // descanso). null significa "sigue, localiza la franja".
    private ResultadoAgregarSubFranja? EvaluarPrecondicionSubFranja()
    {
        if (!_estaActivo)
            return ResultadoAgregarSubFranja.TurnoRetirado;

        if (_turno.EsDescanso())
            return ResultadoAgregarSubFranja.TurnoEsDescanso;

        return null;
    }

    // Programable = completo (CA-ADR-0033).
    internal bool EstaCompleto() => _turno.EstaCompleto();

    // Tell-don't-Ask (MEF-ADR-0012): el catalogo decide si acepta una nueva solicitud, y con que
    // razon -- el handler no interroga su estado interno para decidir por su cuenta.
    internal ResultadoAsignabilidadTurno EvaluarAsignabilidad()
    {
        if (!_estaActivo)
            return ResultadoAsignabilidadTurno.Retirado;

        return EstaCompleto()
            ? ResultadoAsignabilidadTurno.Asignable
            : ResultadoAsignabilidadTurno.Incompleto;
    }

    // Diseno completo resultante, listo para el bus (payload plano, CA-ADR-0025). Null cuando el
    // comando no emitio eventos (no-op): nada que publicar. La version es la del stream despues del
    // cambio: la cargada mas los eventos pendientes.
    internal DisenoDeTurnoActualizado? ObtenerDisenoPublicable()
    {
        if (_uncommittedEvents.Count == 0)
            return null;

        var detalle = _turno.Programar();
        return new DisenoDeTurnoActualizado(
            Guid.Parse(Id!),
            _eventosAplicados,
            _turno.Nombre,
            _turno.EsDescanso(),
            detalle.FranjasOrdinarias.Select(MapearFranja).ToList().AsReadOnly(),
            !_estaActivo);
    }

    private static DetalleFranjaOrdinaria MapearFranja(FranjaProgramada franja) =>
        new(franja.HoraInicio, franja.HoraFin, franja.DiaOffsetFin,
            franja.Descansos.Select(MapearSubFranja).ToList().AsReadOnly(),
            franja.Extras.Select(MapearSubFranja).ToList().AsReadOnly(),
            franja.Descripcion,
            franja.Sede is null
                ? null
                : new DetalleSede(franja.Sede.Id, franja.Sede.Nombre, franja.Sede.CentroDeCostos));

    private static DetalleSubFranja MapearSubFranja(SubFranjaProgramada subFranja) =>
        new(subFranja.HoraInicio, subFranja.HoraFin, subFranja.DiaOffsetInicio,
            subFranja.DiaOffsetFin, subFranja.Descripcion);

    public override string ToString() => _turno.ToString();

    // Tres islas (CA-ADR-0029): devuelve el tipo del dominio; el FA lo mapea a DetalleTurno solo
    // para los eventos que cruzan el bus.
    internal TurnoProgramado ObtenerDetalle() => _turno.Programar();

    // Factory interno: crea el aggregate con el evento en _uncommittedEvents
    // Usado por el handler para StartStream -- no es parte de la interfaz publica del dominio
    internal static CatalogoTurnos Iniciar(TurnoCreado evento)
    {
        var catalogo = new CatalogoTurnos();
        catalogo._uncommittedEvents.Add(evento);
        catalogo.Apply(evento);
        return catalogo;
    }
}
