using Azure.Messaging.ServiceBus;
using Cosmos.EventDriven.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.Infraestructura;

/// <summary>
/// Clase base para FunctionEndpoints de ServiceBus que consumen un evento privado del BC.
/// Contraparte de <see cref="ServiceBusEndpointBase{TEvento}"/> (ADR-0024 decision #8): en vez de
/// traducir el evento a un comando espejo y rutearlo via ICommandRouter, lo despacha directamente
/// al IPrivateEventHandlerAsync via IPrivateEventRouter.
/// Encapsula la orquestacion: deserializar -> despachar al private event router -> complete/lock-lost/dead-letter.
/// Cada endpoint concreto hereda, define [Function] + [ServiceBusTrigger] y delega a <see cref="ProcesarMensaje"/>.
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
        try
        {
            var evento = ServiceBusDeserializador.Deserializar<TPrivateEvent>(message.Body);
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
            logger.LogError(ex, "Error procesando mensaje {MessageId}", message.MessageId);
            await messageActions.DeadLetterMessageAsync(message, cancellationToken: ct);
        }
    }
}
