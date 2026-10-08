using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.AsignarSedeAFranjaFunction.CommandHandler;

// Mismo mecanismo "declinar con resultado" (CA-ADR-0030) que QuitarFranjaCommandHandler: el
// aggregate resuelve ConSede/TieneSedePrearmada; la ArgumentException de sede incompleta la deja
// subir FranjaOrdinaria.Crear (via ConSede), no este handler.
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
    }
}
