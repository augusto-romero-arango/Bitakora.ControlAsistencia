using Azure.Messaging.ServiceBus;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.Infraestructura;

/// <summary>
/// Clase base para FunctionEndpoints de ServiceBus.
/// Encapsula la orquestacion: deserializar -> despachar al command router -> complete/lock-lost/dead-letter.
/// Cada endpoint concreto hereda, define [Function] + [ServiceBusTrigger] y delega a <see cref="ProcesarMensaje"/>.
/// </summary>
public abstract class ServiceBusEndpointBase<TEvento>(ICommandRouter commandRouter, ILogger logger)
    where TEvento : class
{
    protected async Task ProcesarMensaje(
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken ct)
    {
        try
        {
            var evento = ServiceBusDeserializador.Deserializar<TEvento>(message.Body);
            await commandRouter.InvokeAsync(evento, ct);
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
