using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

// HU-4: Aggregate root del catalogo de turnos de trabajo
// ADR-0015: partial class para soportar clase Mensajes en archivo separado
// Interfaz publica: Apply(TurnoCreado), Apply(TurnoRetirado), ToString()
// Estado interno (privado): nombre, franjas ordinarias, activo
public partial class CatalogoTurnos : AggregateRoot
{
    private string _nombre = string.Empty;
    private List<FranjaOrdinaria> _franjasOrdinarias = [];
    private bool _esDescanso;
    private bool _estaActivo;

    // CA-1: aplica TurnoCreado y establece estado interno del aggregate
    // Establece: Id (heredado de AggregateRoot), nombre, franjas ordinarias, activo=true
    public void Apply(TurnoCreado evento)
    {
        Id = evento.TurnoId.ToString();
        _nombre = evento.Nombre;
        _franjasOrdinarias = evento.FranjasOrdinarias.ToList();
        _esDescanso = evento.EsDescanso;
        _estaActivo = true;
    }

    // MEF-ADR-0004 capa 4: no lanza -- la guarda de "ya retirado" decide en Retirar(), antes de
    // emitir.
    public void Apply(TurnoRetirado evento) => _estaActivo = false;

    public void Apply(FranjaAgregada evento) => _franjasOrdinarias.Add(evento.Franja);

    public void Apply(DescansoAgregado evento) => ReemplazarFranja(evento.Franja);

    public void Apply(ExtraAgregado evento) => ReemplazarFranja(evento.Franja);

    // MEF-ADR-0004 capa 4: RemoveAll, no FindIndex + RemoveAt -- sobre un stream anomalo, indexar
    // con -1 lanzaria, y un Apply que lanza deja el aggregate roto para siempre.
    public void Apply(FranjaQuitada evento) =>
        _franjasOrdinarias.RemoveAll(f => f.EmpiezaALaMismaHoraQue(evento.Franja));

    public void Apply(DescansoQuitado evento) => ReemplazarFranja(evento.Franja);

    public void Apply(ExtraQuitado evento) => ReemplazarFranja(evento.Franja);

    // Issue #606: reemplaza la franja por la resultante (con la sede nueva, o sin ella).
    public void Apply(SedeDeFranjaAsignada evento) => throw new NotImplementedException();

    public void Apply(SedeDeFranjaRetirada evento) => throw new NotImplementedException();

    // MEF-ADR-0004 capa 4: localiza por hora de inicio y reemplaza sin invocar ningun factory
    // (ConDescanso/ConExtra), asi que endurecer esas invariantes manana no rompe la rehidratacion
    // de streams viejos. Si ninguna franja empieza a esa hora -- stream anomalo, o franja retirada
    // por un evento posterior -- ignora en vez de indexar con -1: un Apply que lanza deja el
    // aggregate roto para siempre.
    private void ReemplazarFranja(FranjaOrdinaria franja)
    {
        var indice = _franjasOrdinarias.FindIndex(f => f.EmpiezaALaMismaHoraQue(franja));
        if (indice >= 0)
            _franjasOrdinarias[indice] = franja;
    }

    // Mecanismo "declinar con resultado" (CA-ADR-0030): el aggregate nunca lanza -- retorna la
    // razon del rechazo y el handler la traduce al status code (409 Conflict).
    internal ResultadoRetiroTurno Retirar()
    {
        if (!_estaActivo)
            return ResultadoRetiroTurno.YaEstabaRetirado;

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

        if (_esDescanso)
            return ResultadoAgregarFranja.TurnoEsDescanso;

        if (_franjasOrdinarias.Any(f => f.SeSolapaCon(franja)))
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

        var indice = _franjasOrdinarias.FindIndex(f => f.EmpiezaA(horaInicioFranja));
        if (indice == -1)
            return ResultadoAgregarSubFranja.FranjaNoExiste;

        var evento = DescansoAgregado.Crear(
            Guid.Parse(Id!), _franjasOrdinarias[indice].ConDescanso(inicio, fin));
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

        var indice = _franjasOrdinarias.FindIndex(f => f.EmpiezaA(horaInicioFranja));
        if (indice == -1)
            return ResultadoAgregarSubFranja.FranjaNoExiste;

        var evento = ExtraAgregado.Crear(
            Guid.Parse(Id!), _franjasOrdinarias[indice].ConExtra(inicio, fin));
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

        var indice = _franjasOrdinarias.FindIndex(f => f.EmpiezaA(horaInicio));
        if (indice == -1)
            return ResultadoQuitarFranja.FranjaNoExiste;

        var evento = FranjaQuitada.Crear(Guid.Parse(Id!), _franjasOrdinarias[indice]);
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

        var indice = _franjasOrdinarias.FindIndex(f => f.EmpiezaA(horaInicioFranja));
        if (indice == -1)
            return ResultadoQuitarSubFranja.FranjaNoExiste;

        var franjaResultante = _franjasOrdinarias[indice].SinDescanso(horaInicioHija);
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

        var indice = _franjasOrdinarias.FindIndex(f => f.EmpiezaA(horaInicioFranja));
        if (indice == -1)
            return ResultadoQuitarSubFranja.FranjaNoExiste;

        var franjaResultante = _franjasOrdinarias[indice].SinExtra(horaInicioHija);
        if (franjaResultante is null)
            return ResultadoQuitarSubFranja.SubFranjaNoExiste;

        var evento = ExtraQuitado.Crear(Guid.Parse(Id!), franjaResultante);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return ResultadoQuitarSubFranja.Quitada;
    }

    // Issue #606: mismo mecanismo "declinar con resultado" que QuitarFranja -- localiza por hora
    // de inicio y delega en ConSede/TieneSedePrearmada (invariantes del VO ya resueltas por el
    // handler antes de llegar aqui). Precedencia: TurnoRetirado > FranjaNoExiste > FranjaSinSede.
    internal ResultadoAsignarSedeAFranja AsignarSedeAFranja(TimeOnly horaInicioFranja, SedeProgramada? sede) =>
        throw new NotImplementedException();

    // Precondiciones compartidas por AgregarDescanso/AgregarExtra (precedencia: retirado >
    // descanso). null significa "sigue, localiza la franja".
    private ResultadoAgregarSubFranja? EvaluarPrecondicionSubFranja()
    {
        if (!_estaActivo)
            return ResultadoAgregarSubFranja.TurnoRetirado;

        if (_esDescanso)
            return ResultadoAgregarSubFranja.TurnoEsDescanso;

        return null;
    }

    // Un turno es programable cuando esta completo (CA-ADR-0033): declarado descanso, o con al
    // menos una franja ordinaria.
    internal bool EstaCompleto() => _esDescanso || _franjasOrdinarias.Count > 0;

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

    public override string ToString() => (_esDescanso, _franjasOrdinarias.Count) switch
    {
        (true, _) => $"{_nombre} {Mensajes.LabelDescanso}",
        (false, 0) => $"{_nombre} {Mensajes.LabelIncompleto}",
        _ => $"{_nombre} {string.Join("", _franjasOrdinarias)}"
    };

    // Devuelve el turno programado propio del dominio (Programacion.DomainEvents.TurnoProgramado).
    // Issue #319 (tres islas): ya no construye el DTO de bus (DetalleTurno, PrivateEvents) -- el
    // FA mapea TurnoProgramado -> DetalleTurno solo para los eventos que cruzan el bus (CA-5).
    internal TurnoProgramado ObtenerDetalle() => new(
        _nombre,
        _franjasOrdinarias.Select(f => f.ToDetalle()).ToList().AsReadOnly(),
        ToString());

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
