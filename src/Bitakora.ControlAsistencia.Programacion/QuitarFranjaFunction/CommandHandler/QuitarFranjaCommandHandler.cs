using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.QuitarFranjaFunction.CommandHandler;

// Este comando no construye ningun VO, asi que no hay canal de ArgumentException que mezclar con
// el de las reglas de negocio (CA-ADR-0030) -- a diferencia de AgregarFranjaCommandHandler.
public partial class QuitarFranjaCommandHandler : ICommandHandlerAsync<QuitarFranja>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public QuitarFranjaCommandHandler(IEventStore eventStore, IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
    }

    public async Task HandleAsync(QuitarFranja command, CancellationToken ct = default)
    {
        var catalogo = await _eventStore.GetAggregateRootAsync<CatalogoTurnos>(command.TurnoId, ct);
        if (catalogo is null)
            throw new RecursoNoEncontradoException(Mensajes.TurnoNoEncontrado);

        var mensajeDeRechazo = catalogo.QuitarFranja(command.Franja) switch
        {
            ResultadoQuitarFranja.Quitada => null,
            ResultadoQuitarFranja.TurnoRetirado => Mensajes.TurnoRetirado,
            ResultadoQuitarFranja.FranjaNoExiste => Mensajes.FranjaNoExiste,
            var otro => throw new NotSupportedException($"Resultado de QuitarFranja no mapeado: {otro}")
        };

        if (mensajeDeRechazo is not null)
            throw new ReglaDeNegocioDeclinadaException(mensajeDeRechazo);

        var diseno = catalogo.ObtenerDisenoPublicable();
        if (diseno is not null)
            await _privateEventSender.PublishAsync(diseno);
    }
}
