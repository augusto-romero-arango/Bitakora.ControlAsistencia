using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.ControlHoras.ResolverAmbiguedadDeMarcacionesFunction.CommandHandler;

public partial class ResolverAmbiguedadDeMarcacionesCommandHandler : ICommandHandlerAsync<ResolverAmbiguedadDeMarcaciones>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public ResolverAmbiguedadDeMarcacionesCommandHandler(IEventStore eventStore, IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
    }

    public async Task HandleAsync(ResolverAmbiguedadDeMarcaciones command, CancellationToken ct = default)
    {
        var dia = await CargarControlAsync(command.CodigoColaborador, command.Fecha, ct)
            ?? throw new RecursoNoEncontradoException(Mensajes.DiaSinDepuracion);

        var resultado = dia.ValidarReparto(command.Marcaciones);
        if (resultado != ResultadoResolucionAmbiguedad.Valido)
            throw new ReglaDeNegocioDeclinadaException(MensajeDe(resultado));

        var disputas = dia.MarcacionesEnDisputa();
        var porVecino = command.Marcaciones
            .DistinctBy(d => d.Marcacion)
            .GroupBy(d => disputas[d.Marcacion])
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<MarcacionDecidida>)g
                    .OrderBy(d => d.Marcacion)
                    .Select(d => new MarcacionDecidida(d.Marcacion, d.Fecha))
                    .ToList());

        var vecinos = new Dictionary<DiaVecino, ControlDiarioAggregateRoot>();
        foreach (var lado in porVecino.Keys)
            vecinos[lado] = await CargarControlAsync(command.CodigoColaborador, dia.FechaDe(lado), ct)
                ?? throw new RecursoNoEncontradoException(Mensajes.DiaSinDepuracion);

        var calculados = new Dictionary<DateOnly, DiaCalculadoAggregateRoot>();
        foreach (var fecha in vecinos.Values.Select(v => v.Fecha).Append(dia.Fecha))
        {
            var streamDc = DiaCalculadoAggregateRoot.ComputarStreamId(command.CodigoColaborador, fecha);
            if (await _eventStore.ExistsAsync<DiaCalculadoAggregateRoot>(streamDc, ct))
                calculados[fecha] = (await _eventStore.GetAggregateRootAsync<DiaCalculadoAggregateRoot>(streamDc, ct))!;
        }

        if (command.Marcaciones.Any(d => CambiaUsoEnDiaAprobado(d, dia, disputas[d.Marcacion], calculados)))
            throw new ReglaDeNegocioDeclinadaException(Mensajes.CambiaUsoEnDiaAprobado);

        var cambiados = new HashSet<ControlDiarioAggregateRoot>();
        foreach (var (lado, marcaciones) in porVecino)
        {
            if (dia.Resolver(lado, marcaciones)) cambiados.Add(dia);
            var ladoDesdeElVecino = lado == DiaVecino.Vispera ? DiaVecino.DiaSiguiente : DiaVecino.Vispera;
            if (vecinos[lado].Resolver(ladoDesdeElVecino, marcaciones)) cambiados.Add(vecinos[lado]);
        }

        foreach (var control in cambiados)
            await _privateEventSender.PublishAsync(control.CrearDiaDepurado());
    }

    // Manda el dia aprobado: la decision no puede cambiar el uso de la marcacion en su foto aprobada,
    // y con los dos dias del par aprobados siempre declina.
    private static bool CambiaUsoEnDiaAprobado(
        DecisionDeMarcacion decision,
        ControlDiarioAggregateRoot dia,
        DiaVecino lado,
        IReadOnlyDictionary<DateOnly, DiaCalculadoAggregateRoot> calculados)
    {
        var usos = new[] { dia.Fecha, dia.FechaDe(lado) }
            .Select(fecha => (Fecha: fecha, Uso: calculados.TryGetValue(fecha, out var dc)
                ? dc.UsoAprobadoDe(decision.Marcacion)
                : null))
            .Where(u => u.Uso is not null)
            .ToList();

        return usos.Count == 2 || usos.Any(u => u.Uso != (decision.Fecha == u.Fecha));
    }

    private static string MensajeDe(ResultadoResolucionAmbiguedad resultado) => resultado switch
    {
        ResultadoResolucionAmbiguedad.RepartoIncompleto => Mensajes.RepartoIncompleto,
        ResultadoResolucionAmbiguedad.MarcacionNoAmbigua => Mensajes.MarcacionNoAmbigua,
        _ => Mensajes.FechaNoValida
    };

    private async Task<ControlDiarioAggregateRoot?> CargarControlAsync(
        string codigoColaborador, DateOnly fecha, CancellationToken ct)
    {
        var streamId = ControlDiarioAggregateRoot.ComputarStreamId(codigoColaborador, fecha);
        return await _eventStore.ExistsAsync<ControlDiarioAggregateRoot>(streamId, ct)
            ? await _eventStore.GetAggregateRootAsync<ControlDiarioAggregateRoot>(streamId, ct)
            : null;
    }
}
