using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

// Segundo nivel de composicion sobre el Turno (CA-ADR-0034). El estado que aun no tiene consumidor
// (_nombre) entra con el, no antes.
// Anatomia de clave (CA-ADR-0031): Guid canonico "D", sin prefijo.
public partial class PlantillaSemanalTurnos : AggregateRoot
{
    private int _semanas;
    private bool _estaActiva;
    private sealed record DiaAsignado(Guid TurnoId, Turno Turno, long VersionTurno, bool Retirado = false);

    private readonly Dictionary<(int Semana, DiaSemana Dia), DiaAsignado> _dias = new();

    public void Apply(PlantillaSemanalCreada evento)
    {
        Id = evento.PlantillaId.ToString();
        _semanas = evento.Semanas;
        _estaActiva = true;
    }

    public void Apply(DiaDePlantillaSemanalAsignado evento) =>
        _dias[(evento.Semana, evento.Dia)] =
            new DiaAsignado(evento.TurnoId, evento.Turno, evento.VersionTurno);

    // Remove sobre una clave ausente devuelve false sin lanzar (MEF-ADR-0004 capa 4).
    public void Apply(DiaDePlantillaSemanalQuitado evento) => _dias.Remove((evento.Semana, evento.Dia));

    public void Apply(TurnoDePlantillaSemanalSincronizado evento)
    {
        foreach (var clave in _dias.Where(d => d.Value.TurnoId == evento.TurnoId).Select(d => d.Key).ToList())
            _dias[clave] = new DiaAsignado(evento.TurnoId, evento.Turno, evento.VersionTurno, evento.Retirado);
    }

    public void Apply(PlantillaSemanalRetirada evento) => _estaActiva = false;

    public void Apply(JornadaDePlantillaSemanalAsignada evento)
    {
        JornadaId = evento.JornadaId;
        Limites = evento.Limites;
        VersionJornada = evento.VersionJornada;
    }

    public void Apply(JornadaDePlantillaSemanalQuitada evento)
    {
        JornadaId = null;
        Limites = null;
        VersionJornada = 0;
    }

    public void Apply(AdvertenciasDePlantillaSemanalCalculadas evento) => Advertencias = evento.Advertencias;

    internal IReadOnlyList<AdvertenciaPlantillaSemanal> Advertencias { get; private set; } = [];

    internal long VersionDelTurno(Guid turnoId) =>
        _dias.Values.Where(d => d.TurnoId == turnoId).Select(d => d.VersionTurno).DefaultIfEmpty(0).Max();

    private bool UsaElTurno(Guid turnoId) => _dias.Values.Any(d => d.TurnoId == turnoId);

    // Declina con resultado (CA-ADR-0030). Una version menor o igual a la vigente (desorden del bus,
    // reentrega) o un turno que ningun dia usa no emiten nada (CA-ADR-0034).
    internal ResultadoSincronizarTurno SincronizarTurno(Guid turnoId, Turno copia, long version, bool retirado)
    {
        if (!_estaActiva)
            return ResultadoSincronizarTurno.PlantillaRetirada;

        if (!UsaElTurno(turnoId) || VersionDelTurno(turnoId) >= version)
            return ResultadoSincronizarTurno.SinCambios;

        var evento = TurnoDePlantillaSemanalSincronizado.Crear(Guid.Parse(Id), turnoId, copia, version, retirado);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        Auditar();
        return ResultadoSincronizarTurno.Sincronizado;
    }

    internal Guid? JornadaId { get; private set; }

    internal LimitesJornada? Limites { get; private set; }

    internal long VersionJornada { get; private set; }

    // Declina con resultado (CA-ADR-0030). Precedencia: retirada > sin cambios > asignada. La copia
    // atrasada (version menor a la ofrecida) es la autocorreccion y emite el evento.
    internal ResultadoAsignarJornada AsignarJornada(Guid jornadaId, LimitesJornada limites, long version)
    {
        if (!_estaActiva)
            return ResultadoAsignarJornada.PlantillaRetirada;

        if (JornadaId == jornadaId && VersionJornada >= version)
            return ResultadoAsignarJornada.SinCambios;

        var evento = JornadaDePlantillaSemanalAsignada.Crear(Guid.Parse(Id), jornadaId, limites, version);
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

        var evento = JornadaDePlantillaSemanalQuitada.Crear(Guid.Parse(Id));
        _uncommittedEvents.Add(evento);
        Apply(evento);
        Auditar();
        return ResultadoQuitarJornada.Quitada;
    }

    private void Auditar()
    {
        var calculadas = AuditoriaPlantillaSemanal.Auditar(
            _semanas,
            _dias.Select(d => new DiaDePlantillaAuditado(d.Key.Semana, d.Key.Dia, d.Value.Turno, d.Value.Retirado)),
            Limites);
        if (calculadas.SequenceEqual(Advertencias))
            return;

        var evento = AdvertenciasDePlantillaSemanalCalculadas.Crear(Guid.Parse(Id), calculadas);
        _uncommittedEvents.Add(evento);
        Apply(evento);
    }

    // Nace ya con su Jornada: una sola auditoria sobre el estado final, sin el PlantillaSinJornada intermedio.
    internal static PlantillaSemanalTurnos IniciarConJornada(
        PlantillaSemanalCreada evento, Guid jornadaId, LimitesJornada limites, long version)
    {
        var plantilla = new PlantillaSemanalTurnos();
        plantilla._uncommittedEvents.Add(evento);
        plantilla.Apply(evento);
        var asignada = JornadaDePlantillaSemanalAsignada.Crear(evento.PlantillaId, jornadaId, limites, version);
        plantilla._uncommittedEvents.Add(asignada);
        plantilla.Apply(asignada);
        plantilla.Auditar();
        return plantilla;
    }

    internal static PlantillaSemanalTurnos Iniciar(PlantillaSemanalCreada evento)
    {
        var plantilla = new PlantillaSemanalTurnos();
        plantilla._uncommittedEvents.Add(evento);
        plantilla.Apply(evento);
        plantilla.Auditar();
        return plantilla;
    }

    // Precedencia: plantilla retirada > semana fuera de rango > sin cambios > asignado. Una copia
    // atrasada (version menor a la ofrecida) es la autocorreccion y emite el evento.
    internal ResultadoAsignarDia AsignarDia(int semana, DiaSemana dia, Guid turnoId, Turno copia, long versionTurno)
    {
        if (!_estaActiva)
            return ResultadoAsignarDia.PlantillaRetirada;

        if (semana > _semanas)
            return ResultadoAsignarDia.SemanaFueraDeRango;

        if (_dias.TryGetValue((semana, dia), out var actual)
            && actual.TurnoId == turnoId && actual.VersionTurno >= versionTurno)
            return ResultadoAsignarDia.SinCambios;

        var evento = DiaDePlantillaSemanalAsignado.Crear(Guid.Parse(Id), semana, dia, turnoId, copia, versionTurno);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        Auditar();
        return ResultadoAsignarDia.Asignado;
    }

    // Declina con resultado, nunca lanza (CA-ADR-0030). La precedencia es parte del contrato:
    // plantilla retirada > semana fuera de rango > sin cambios; la semana se valida antes que el
    // estado del dia, aunque ese dia ya este vacio.
    internal ResultadoQuitarDia QuitarDia(int semana, DiaSemana dia)
    {
        if (!_estaActiva)
            return ResultadoQuitarDia.PlantillaRetirada;

        if (semana > _semanas)
            return ResultadoQuitarDia.SemanaFueraDeRango;

        if (!_dias.ContainsKey((semana, dia)))
            return ResultadoQuitarDia.SinCambios;

        var evento = DiaDePlantillaSemanalQuitado.Crear(Guid.Parse(Id), semana, dia);
        _uncommittedEvents.Add(evento);
        Apply(evento);
        Auditar();
        return ResultadoQuitarDia.Quitado;
    }

    // Declina con resultado, nunca lanza (CA-ADR-0030). Retirar una plantilla ya retirada es
    // SinCambios (204 idempotente), no el 409 del precedente CatalogoTurnos.Retirar.
    internal ResultadoRetiroPlantilla Retirar()
    {
        if (!_estaActiva)
            return ResultadoRetiroPlantilla.SinCambios;

        var evento = PlantillaSemanalRetirada.Crear(Guid.Parse(Id));
        _uncommittedEvents.Add(evento);
        Apply(evento);
        return ResultadoRetiroPlantilla.Retirada;
    }
}
