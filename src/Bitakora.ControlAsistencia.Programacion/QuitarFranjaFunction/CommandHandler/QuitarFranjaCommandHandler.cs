using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.QuitarFranjaFunction.CommandHandler;

// No construye ningun VO: no hay ArgumentException que mezclar con las reglas de negocio (CA-ADR-0030).
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
            ResultadoQuitarFranja.TurnoDeHorarioAbierto => Mensajes.TurnoDeHorarioAbierto,
            ResultadoQuitarFranja.FranjaNoExiste => Mensajes.FranjaNoExiste,
            ResultadoQuitarFranja.DejariaVentanaSinFranjas => Mensajes.DejariaVentanaSinFranjas,
            var otro => throw new NotSupportedException($"Resultado de QuitarFranja no mapeado: {otro}")
        };

        if (mensajeDeRechazo is not null)
            throw new ReglaDeNegocioDeclinadaException(mensajeDeRechazo);

        var diseno = catalogo.ObtenerDisenoPublicable();
        if (diseno is not null)
            await _privateEventSender.PublishAsync(diseno);
    }
}
