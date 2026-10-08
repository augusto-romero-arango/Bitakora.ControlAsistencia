using Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.QuitarSubFranjaFunction.CommandHandler;

// Este comando no construye ningun VO, asi que no hay canal de ArgumentException que mezclar con
// el de las reglas de negocio (CA-ADR-0030) -- a diferencia de AgregarSubFranjaCommandHandler.
public partial class QuitarSubFranjaCommandHandler : ICommandHandlerAsync<QuitarSubFranja>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public QuitarSubFranjaCommandHandler(IEventStore eventStore, IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
    }

    public async Task HandleAsync(QuitarSubFranja command, CancellationToken ct = default)
    {
        var catalogo = await _eventStore.GetAggregateRootAsync<CatalogoTurnos>(command.TurnoId, ct);
        if (catalogo is null)
            throw new RecursoNoEncontradoException(Mensajes.TurnoNoEncontrado);

        var resultado = command.Tipo switch
        {
            TipoSubFranja.Descanso => catalogo.QuitarDescanso(command.Franja, command.Inicio),
            TipoSubFranja.Extra => catalogo.QuitarExtra(command.Franja, command.Inicio),
            var otro => throw new NotSupportedException($"Tipo de sub-franja no mapeado: {otro}")
        };

        var mensajeDeRechazo = resultado switch
        {
            ResultadoQuitarSubFranja.Quitada => null,
            ResultadoQuitarSubFranja.TurnoRetirado => Mensajes.TurnoRetirado,
            ResultadoQuitarSubFranja.FranjaNoExiste => Mensajes.FranjaNoExiste,
            ResultadoQuitarSubFranja.SubFranjaNoExiste => Mensajes.SubFranjaNoExiste,
            var otro => throw new NotSupportedException($"Resultado de QuitarSubFranja no mapeado: {otro}")
        };

        if (mensajeDeRechazo is not null)
            throw new ReglaDeNegocioDeclinadaException(mensajeDeRechazo);
    }
}
