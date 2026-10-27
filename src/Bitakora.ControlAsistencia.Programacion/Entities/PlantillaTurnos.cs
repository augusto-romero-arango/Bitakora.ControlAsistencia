using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

public partial class PlantillaTurnos : AggregateRoot
{
    private sealed record DiaAsignado(Guid TurnoId, Turno Turno, long VersionTurno, bool Retirado = false);

    private int _diasDelCiclo;
    private bool _estaActiva;
    private readonly Dictionary<int, DiaAsignado> _dias = new();

    public void Apply(PlantillaDeTurnosCreada evento)
    {
        Id = evento.PlantillaId.ToString();
        _diasDelCiclo = evento.DiasDelCiclo;
        _estaActiva = true;
    }

    public void Apply(DiaDePlantillaDeTurnosAsignado evento) =>
        _dias[evento.Dia] = new DiaAsignado(evento.TurnoId, evento.Turno, evento.VersionTurno);

    public void Apply(PlantillaDeTurnosRetirada evento) => _estaActiva = false;

    public void Apply(JornadaDePlantillaDeTurnosAsignada evento)
    {
        JornadaId = evento.JornadaId;
        Limites = evento.Limites;
        VersionJornada = evento.VersionJornada;
    }

    public void Apply(JornadaDePlantillaDeTurnosQuitada evento)
    {
        JornadaId = null;
        Limites = null;
        VersionJornada = 0;
    }

    public void Apply(AdvertenciasDePlantillaDeTurnosCalculadas evento) => Advertencias = evento.Advertencias;

    internal IReadOnlyList<AdvertenciaPlantillaTurnos> Advertencias { get; private set; } = [];

    internal Guid? JornadaId { get; private set; }

    internal LimitesJornada? Limites { get; private set; }

    internal long VersionJornada { get; private set; }

    internal ResultadoAsignarJornada AsignarJornada(Guid jornadaId, LimitesJornada limites, long version)
    {
        if (!_estaActiva)
            return ResultadoAsignarJornada.PlantillaRetirada;

        if (JornadaId == jornadaId && VersionJornada >= version)
            return ResultadoAsignarJornada.SinCambios;

        var evento = JornadaDePlantillaDeTurnosAsignada.Crear(Guid.Parse(Id), jornadaId, limites, version);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        Auditar();
        return ResultadoAsignarJornada.Asignada;
    }

    internal ResultadoQuitarJornada QuitarJornada()
    {
        if (!_estaActiva)
            return ResultadoQuitarJornada.PlantillaRetirada;

        if (JornadaId is null)
            return ResultadoQuitarJornada.SinCambios;

        var evento = JornadaDePlantillaDeTurnosQuitada.Crear(Guid.Parse(Id));
        _uncommittedEvents.Add(evento);
        Apply(evento);
        Auditar();
        return ResultadoQuitarJornada.Quitada;
    }

    public void Apply(TurnoDePlantillaDeTurnosSincronizado evento)
    {
        foreach (var (dia, actual) in _dias.Where(d => d.Value.TurnoId == evento.TurnoId).ToList())
            _dias[dia] = actual with { Turno = evento.Turno, VersionTurno = evento.VersionTurno, Retirado = evento.Retirado };
    }

    internal long VersionDelTurno(Guid turnoId) =>
        _dias.Values.Where(d => d.TurnoId == turnoId).Select(d => d.VersionTurno).DefaultIfEmpty(0).Max();

    internal bool DiaEstaRetirado(int dia) => _dias.TryGetValue(dia, out var d) && d.Retirado;

    private bool UsaElTurno(Guid turnoId) => _dias.Values.Any(d => d.TurnoId == turnoId);

    internal ResultadoSincronizarTurno SincronizarTurno(Guid turnoId, Turno copia, long version, bool retirado)
    {
        if (!_estaActiva)
            return ResultadoSincronizarTurno.PlantillaRetirada;

        if (!UsaElTurno(turnoId) || VersionDelTurno(turnoId) >= version)
            return ResultadoSincronizarTurno.SinCambios;

        var evento = TurnoDePlantillaDeTurnosSincronizado.Crear(Guid.Parse(Id), turnoId, copia, version, retirado);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        Auditar();
        return ResultadoSincronizarTurno.Sincronizado;
    }

    internal ResultadoRetiroPlantilla Retirar()
    {
        if (!_estaActiva)
            return ResultadoRetiroPlantilla.SinCambios;

        var evento = PlantillaDeTurnosRetirada.Crear(Guid.Parse(Id));
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return ResultadoRetiroPlantilla.Retirada;
    }

    private void Auditar()
    {
        var calculadas = AuditoriaPlantillaTurnos.Auditar(
            _diasDelCiclo,
            _dias.Select(d => new DiaDePlantillaTurnosAuditado(d.Key, d.Value.Turno, d.Value.Retirado)),
            Limites);
        if (calculadas.SequenceEqual(Advertencias))
            return;

        var evento = AdvertenciasDePlantillaDeTurnosCalculadas.Crear(Guid.Parse(Id), calculadas);
        _uncommittedEvents.Add(evento);
        Apply(evento);
    }

    // Mismo turno con copia atrasada no es no-op: reasignar autocorrige la copia (CA-ADR-0034, enmienda #886).
    internal ResultadoAsignarDiaDePlantillaTurnos AsignarDia(int dia, Guid turnoId, Turno copia, long versionTurno)
    {
        if (!_estaActiva)
            return ResultadoAsignarDiaDePlantillaTurnos.PlantillaRetirada;

        if (dia > _diasDelCiclo)
            return ResultadoAsignarDiaDePlantillaTurnos.DiaFueraDelCiclo;

        if (_dias.TryGetValue(dia, out var actual)
            && actual.TurnoId == turnoId && actual.VersionTurno >= versionTurno)
            return ResultadoAsignarDiaDePlantillaTurnos.SinCambios;

        var evento = DiaDePlantillaDeTurnosAsignado.Crear(Guid.Parse(Id), dia, turnoId, copia, versionTurno);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        Auditar();
        return ResultadoAsignarDiaDePlantillaTurnos.Asignado;
    }

    public static PlantillaTurnos Iniciar(PlantillaDeTurnosCreada evento)
    {
        var plantilla = new PlantillaTurnos();
        plantilla._uncommittedEvents.Add(evento);
        plantilla.Apply(evento);
        plantilla.Auditar();
        return plantilla;
    }
}
