using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.AsignarSedeAFranjaFunction.CommandHandler;

// La ArgumentException de sede incompleta la lanza FranjaOrdinaria.Crear (via ConSede), no este
// handler; el aggregate solo declina con resultado (CA-ADR-0030).
public partial class AsignarSedeAFranjaCommandHandler : ICommandHandlerAsync<AsignarSedeAFranja>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public AsignarSedeAFranjaCommandHandler(IEventStore eventStore, IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
    }

    public async Task HandleAsync(AsignarSedeAFranja command, CancellationToken ct = default)
    {
        var catalogo = await _eventStore.GetAggregateRootAsync<CatalogoTurnos>(command.TurnoId, ct);
        if (catalogo is null)
            throw new RecursoNoEncontradoException(Mensajes.TurnoNoEncontrado);

        var mensajeDeRechazo = catalogo.AsignarSedeAFranja(command.Franja, command.Sede) switch
        {
            ResultadoAsignarSedeAFranja.Asignada => null,
            ResultadoAsignarSedeAFranja.Retirada => null,
            ResultadoAsignarSedeAFranja.TurnoRetirado => Mensajes.TurnoRetirado,
            ResultadoAsignarSedeAFranja.FranjaNoExiste => Mensajes.FranjaNoExiste,
            ResultadoAsignarSedeAFranja.FranjaSinSede => Mensajes.FranjaSinSede,
            var otro => throw new NotSupportedException($"Resultado de AsignarSedeAFranja no mapeado: {otro}")
        };

        if (mensajeDeRechazo is not null)
            throw new ReglaDeNegocioDeclinadaException(mensajeDeRechazo);

        var diseno = catalogo.ObtenerDisenoPublicable();
        if (diseno is not null)
            await _privateEventSender.PublishAsync(diseno);
    }
}
