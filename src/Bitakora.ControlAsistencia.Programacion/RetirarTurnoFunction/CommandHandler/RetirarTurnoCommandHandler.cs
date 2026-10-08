using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.RetirarTurnoFunction.CommandHandler;

public partial class RetirarTurnoCommandHandler : ICommandHandlerAsync<RetirarTurno>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public RetirarTurnoCommandHandler(IEventStore eventStore, IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
    }

    public async Task HandleAsync(RetirarTurno command, CancellationToken ct = default)
    {
        var catalogo = await _eventStore.GetAggregateRootAsync<CatalogoTurnos>(command.TurnoId, ct);
        if (catalogo is null)
            throw new RecursoNoEncontradoException(Mensajes.TurnoNoEncontrado);

        catalogo.Retirar();

        var diseno = catalogo.ObtenerDisenoPublicable();
        if (diseno is not null)
            await _privateEventSender.PublishAsync(diseno);
    }
}
