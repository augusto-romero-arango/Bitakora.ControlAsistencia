using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.ControlHoras.RepartirMarcacionesCuandoDiaDepuradoFunction.CommandHandler;

public partial class RepartirMarcacionesCommandHandler : ICommandHandlerAsync<RepartirMarcaciones>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public RepartirMarcacionesCommandHandler(IEventStore eventStore, IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
    }

    public async Task HandleAsync(RepartirMarcaciones command, CancellationToken ct = default)
    {
        var dia = await CargarAsync(command.CodigoColaborador, command.Fecha, ct);
        if (dia is null) return;

        var cambiados = new HashSet<ControlDiarioAggregateRoot>();
        foreach (var fechaVecino in new[] { command.Fecha.AddDays(-1), command.Fecha.AddDays(1) })
        {
            var vecino = await CargarAsync(command.CodigoColaborador, fechaVecino, ct);
            if (vecino is null) continue;

            // El anterior toma primero el inicio del posterior (solo depende de su turno): asi el
            // posterior recibe el fin del anterior ya recalculado, sin esperar otra ronda de DiaDepurado.
            var (anterior, posterior) = fechaVecino < command.Fecha ? (vecino, dia) : (dia, vecino);
            if (anterior.ActualizarCoberturaVecina(posterior)) cambiados.Add(anterior);
            if (posterior.ActualizarCoberturaVecina(anterior)) cambiados.Add(posterior);
            if (vecino.RecogerDe(dia)) cambiados.Add(vecino);
            if (dia.RecogerDe(vecino)) cambiados.Add(dia);
        }

        foreach (var control in cambiados)
            await _privateEventSender.PublishAsync(control.CrearDiaDepurado());
    }

    // Un vecino sin stream no recibe nada y no se crea.
    private async Task<ControlDiarioAggregateRoot?> CargarAsync(
        string codigoColaborador, DateOnly fecha, CancellationToken ct)
    {
        var streamId = ControlDiarioAggregateRoot.ComputarStreamId(codigoColaborador, fecha);
        return await _eventStore.ExistsAsync<ControlDiarioAggregateRoot>(streamId, ct)
            ? await _eventStore.GetAggregateRootAsync<ControlDiarioAggregateRoot>(streamId, ct)
            : null;
    }
}
