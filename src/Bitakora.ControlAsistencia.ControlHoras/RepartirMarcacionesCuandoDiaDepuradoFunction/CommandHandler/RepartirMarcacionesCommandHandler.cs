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

            cambiados.UnionWith(dia.RepartirCon(vecino));
        }

        foreach (var control in cambiados)
        {
            var diaDepurado = control.CrearDiaDepurado();
            await _privateEventSender.PublishAsync(new PublishOptions { GroupId = diaDepurado.CodigoColaborador }, diaDepurado);
        }
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
