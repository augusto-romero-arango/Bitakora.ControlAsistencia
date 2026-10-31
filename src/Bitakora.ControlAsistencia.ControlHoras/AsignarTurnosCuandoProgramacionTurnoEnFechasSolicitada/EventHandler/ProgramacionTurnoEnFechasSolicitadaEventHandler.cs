using System.Text.Json;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;
using ResumenColaborador = Bitakora.ControlAsistencia.PrivateEvents.Colaboradores.ResumenColaborador;

namespace Bitakora.ControlAsistencia.ControlHoras.AsignarTurnosCuandoProgramacionTurnoEnFechasSolicitada.EventHandler;

public partial class ProgramacionTurnoEnFechasSolicitadaEventHandler
    : IPrivateEventHandlerAsync<ProgramacionTurnoEnFechasSolicitada>
{
    // Service Bus Standard admite 256 KB por mensaje y las propiedades pueden ocupar hasta 64 KB.
    private const int TopeCuerpoEnBytes = 192 * 1024;
    // Holgura para lo que DiasDepurados agrega alrededor de las fotos (CodigoColaborador y llaves).
    private const int EnvolturaEnBytes = 256;

    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public ProgramacionTurnoEnFechasSolicitadaEventHandler(
        IEventStore eventStore,
        IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
    }

    public async Task HandleAsync(ProgramacionTurnoEnFechasSolicitada @event, CancellationToken ct = default)
    {
        var codigo = @event.Colaborador.CodigoColaborador;
        var fechasDelLote = @event.Fechas.Distinct().Order().ToList();
        var delLote = fechasDelLote.ToHashSet();

        var dias = new SortedDictionary<DateOnly, ControlDiarioAggregateRoot>();
        var nuevos = new List<ControlDiarioAggregateRoot>();
        foreach (var fecha in fechasDelLote)
        {
            var (control, esNuevo) = await AsignarAsync(@event, fecha, ct);
            dias[fecha] = control;
            if (esNuevo) nuevos.Add(control);
        }

        var vecinosDeAfuera = new Dictionary<DateOnly, ControlDiarioAggregateRoot>();
        foreach (var fecha in fechasDelLote.SelectMany(f => new[] { f.AddDays(-1), f.AddDays(1) })
                     .Where(f => !delLote.Contains(f)).Distinct())
        {
            var vecino = await CargarAsync(codigo, fecha, ct);
            if (vecino is null) continue;
            vecinosDeAfuera[fecha] = vecino;
            dias[fecha] = vecino;
        }

        var cambiadosDeAfuera = Repartir(dias);

        // StartStream toma los eventos pendientes al llamarse: va despues del reparto, que agrega
        // eventos a los dias nuevos.
        foreach (var nuevo in nuevos)
            _eventStore.StartStream(nuevo);

        foreach (var fotos in PartirPorTamano(fechasDelLote.Select(f => dias[f].CrearDiaDepurado())))
            await _privateEventSender.PublishAsync(
                new PublishOptions { GroupId = codigo }, new DiasDepurados(codigo, fotos));

        foreach (var vecino in vecinosDeAfuera.Values.Where(cambiadosDeAfuera.Contains))
        {
            var foto = vecino.CrearDiaDepurado();
            await _privateEventSender.PublishAsync(new PublishOptions { GroupId = codigo }, foto);
        }
    }

    // Reparto en memoria sobre cada par de dias calendario consecutivos, en pasadas completas hasta
    // que ninguna cambia nada (CA-ADR-0043): reproduce la cascada de DiaDepurado sin el bus.
    private static HashSet<ControlDiarioAggregateRoot> Repartir(SortedDictionary<DateOnly, ControlDiarioAggregateRoot> dias)
    {
        var acumulados = new HashSet<ControlDiarioAggregateRoot>();
        var pares = dias.Where(d => dias.ContainsKey(d.Key.AddDays(1)))
            .Select(d => (Anterior: d.Value, Posterior: dias[d.Key.AddDays(1)]))
            .ToList();

        var maximoDePasadas = dias.Count * 2 + 2;
        for (var pasada = 0; pasada < maximoDePasadas; pasada++)
        {
            var cambiados = pares.SelectMany(p => p.Anterior.RepartirCon(p.Posterior)).ToList();
            if (cambiados.Count == 0) break;
            acumulados.UnionWith(cambiados);
        }

        return acumulados;
    }

    // Una foto que sola excede el tope viaja en su propio mensaje.
    private static IEnumerable<IReadOnlyList<DiaDepurado>> PartirPorTamano(IEnumerable<DiaDepurado> fotos)
    {
        var grupo = new List<DiaDepurado>();
        var tamanoGrupo = EnvolturaEnBytes;
        foreach (var foto in fotos)
        {
            // +1 por la coma que separa las fotos dentro del arreglo.
            var tamano = JsonSerializer.SerializeToUtf8Bytes(foto).Length + 1;
            var grupoLleno = grupo.Count > 0 && tamanoGrupo + tamano > TopeCuerpoEnBytes;
            if (grupoLleno)
            {
                yield return grupo;
                grupo = [];
                tamanoGrupo = EnvolturaEnBytes;
            }
            grupo.Add(foto);
            tamanoGrupo += tamano;
        }

        if (grupo.Count > 0) yield return grupo;
    }

    private async Task<(ControlDiarioAggregateRoot Control, bool EsNuevo)> AsignarAsync(
        ProgramacionTurnoEnFechasSolicitada solicitud, DateOnly fecha, CancellationToken ct)
    {
        var streamId = ControlDiarioAggregateRoot.ComputarStreamId(solicitud.Colaborador.CodigoColaborador, fecha);
        var evento = new TurnoDiarioAsignado(
            streamId, MapearColaboradorProgramado(solicitud.Colaborador), fecha,
            MapearTurnoDiario(solicitud.DetalleTurno), solicitud.SolicitudId,
            MapearJornada(solicitud.Jornada));

        var existente = await CargarAsync(solicitud.Colaborador.CodigoColaborador, fecha, ct);
        if (existente is not null)
        {
            existente.AsignarTurnoDeLote(evento);
            return (existente, false);
        }

        return (ControlDiarioAggregateRoot.Iniciar(evento), true);
    }

    private async Task<ControlDiarioAggregateRoot?> CargarAsync(
        string codigoColaborador, DateOnly fecha, CancellationToken ct)
    {
        var streamId = ControlDiarioAggregateRoot.ComputarStreamId(codigoColaborador, fecha);
        return await _eventStore.ExistsAsync<ControlDiarioAggregateRoot>(streamId, ct)
            ? await _eventStore.GetAggregateRootAsync<ControlDiarioAggregateRoot>(streamId, ct)
            : null;
    }

    private static ColaboradorProgramado MapearColaboradorProgramado(ResumenColaborador colaborador) =>
        new(colaborador.Identificacion, colaborador.CodigoColaborador, colaborador.NombreCompleto);

    private static TurnoDiario MapearTurnoDiario(DetalleTurno turno) =>
        new(turno.Nombre, turno.FranjasOrdinarias.Select(MapearFranja).ToList(), turno.Descripcion,
            turno.AmplitudVentanaEntradaEnMinutos,
            turno.EsHorarioAbierto);

    private static FranjaProgramada MapearFranja(DetalleFranjaOrdinaria franja) =>
        new(franja.HoraInicio, franja.HoraFin, franja.DiaOffsetFin,
            franja.Descansos.Select(MapearSubFranja).ToList(),
            franja.Extras.Select(MapearSubFranja).ToList(),
            franja.Descripcion,
            MapearSede(franja.Sede));

    private static SedeProgramada? MapearSede(DetalleSede? sede) =>
        sede is null ? null : new SedeProgramada(sede.Id, sede.Nombre, sede.CentroDeCostos);

    private static JornadaProgramada? MapearJornada(DetalleJornada? jornada) =>
        jornada is null
            ? null
            : new JornadaProgramada(jornada.JornadaId, jornada.HorasSemanalesEnMinutos,
                jornada.TopeDiarioEnMinutos, jornada.MinimoDiarioEnMinutos, jornada.DiasDescansoPorSemana);

    private static SubFranjaProgramada MapearSubFranja(DetalleSubFranja sub) =>
        new(sub.HoraInicio, sub.HoraFin, sub.DiaOffsetInicio, sub.DiaOffsetFin, sub.Descripcion);
}
