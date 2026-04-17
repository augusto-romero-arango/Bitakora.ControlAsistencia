using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction.CommandHandler;

// HU-105: Handler del comando RegistrarMarcacion
// Flujo: verificar idempotencia via ExistsAsync -> normalizar timestamp -> persistir -> publicar
// CA-4: si el stream ya existe (duplicado exacto), retornar silenciosamente sin persistir ni publicar
// CA-8: tras persistir exitosamente, publicar MarcacionRegistrada via IPrivateEventSender
// ADR-0015: partial class para soportar clase Mensajes en archivo separado si se requiere
public partial class RegistrarMarcacionCommandHandler : ICommandHandlerAsync<RegistrarMarcacion>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public RegistrarMarcacionCommandHandler(IEventStore eventStore, IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
    }

    public Task HandleAsync(RegistrarMarcacion command, CancellationToken ct = default)
        => throw new NotImplementedException();
}
