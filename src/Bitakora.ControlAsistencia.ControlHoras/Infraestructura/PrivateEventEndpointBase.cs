using Azure.Messaging.ServiceBus;
using Cosmos.EventDriven.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.Infraestructura;

/// <summary>
/// Base de los endpoints de Service Bus que consumen un evento privado sin comando espejo: lo
/// despacha al IPrivateEventHandlerAsync via IPrivateEventRouter. Mensaje ilegible -> dead letter;
/// fallo del handler -> abandon para reintento; lock perdido -> solo warning.
/// </summary>
public abstract class PrivateEventEndpointBase<TPrivateEvent>(
    IPrivateEventRouter privateEventRouter, ILogger logger)
    where TPrivateEvent : class, IPrivateEvent
{
    protected async Task ProcesarMensaje(
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken ct)
    {
        TPrivateEvent evento;
        try
        {
            evento = ServiceBusDeserializador.Deserializar<TPrivateEvent>(message.Body);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Mensaje ilegible {MessageId} - se envia a dead letter", message.MessageId);
            await messageActions.DeadLetterMessageAsync(message, cancellationToken: ct);
            return;
        }

        try
        {
            await privateEventRouter.InvokeAsync(evento, ct);
            await messageActions.CompleteMessageAsync(message, ct);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessageLockLost)
        {
            logger.LogWarning(ex,
                "Lock perdido para mensaje {MessageId} - Service Bus lo re-entregara automaticamente",
                message.MessageId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error procesando mensaje {MessageId} (DeliveryCount {DeliveryCount}) - se abandona para reintento",
                message.MessageId, message.DeliveryCount);
            await messageActions.AbandonMessageAsync(message, cancellationToken: ct);
        }
    }
}
