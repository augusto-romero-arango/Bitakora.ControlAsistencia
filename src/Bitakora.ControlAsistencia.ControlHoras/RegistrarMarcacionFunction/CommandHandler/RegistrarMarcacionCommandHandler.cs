using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction.CommandHandler;

public partial class RegistrarMarcacionCommandHandler : ICommandHandlerAsync<RegistrarMarcacion>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public RegistrarMarcacionCommandHandler(IEventStore eventStore, IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
    }

    public async Task HandleAsync(RegistrarMarcacion command, CancellationToken ct = default)
    {
        var streamId = RegistroDeMarcacionAggregateRoot.ComputarStreamId(
            command.CodigoColaborador, command.Timestamp);

        // Duplicado exacto: retorno silencioso, sin persistir ni publicar.
        var existe = await _eventStore.ExistsAsync<RegistroDeMarcacionAggregateRoot>(streamId, ct);
        if (existe)
            return;

        // El factory trunca el timestamp al minuto y valida CodigoColaborador. Si falla, el throw
        // ocurre aqui (borde del handler), no dentro del aggregate (MEF-ADR-0004).
        var evento = MarcacionRegistrada.Crear(
            command.CodigoColaborador,
            command.Timestamp,
            command.TipoMarcacion,
            command.DispositivoId);

        var registro = RegistroDeMarcacionAggregateRoot.Iniciar(streamId, command.Timestamp, evento);
        _eventStore.StartStream(registro);

        // Se publica el contrato de bus, no el evento de dominio.
        var registroCreado = registro.CrearRegistroDeMarcacionCreado();
        await _privateEventSender.PublishAsync(new PublishOptions { GroupId = registroCreado.CodigoColaborador }, registroCreado);
    }
}
