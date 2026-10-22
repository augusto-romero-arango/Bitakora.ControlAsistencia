using Azure.Messaging.ServiceBus;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.Infraestructura;

/// <summary>
/// Clase base para FunctionEndpoints de ServiceBus.
/// Encapsula la orquestacion: deserializar -> despachar al command router -> complete/lock-lost/dead-letter.
/// Cada endpoint concreto hereda, define [Function] + [ServiceBusTrigger] y delega a <see cref="ProcesarMensaje"/>,
/// o a la sobrecarga que construye el comando a partir del evento cuando no son el mismo tipo.
/// </summary>
public abstract class ServiceBusEndpointBase<TEvento>(ICommandRouter commandRouter, ILogger logger)
    where TEvento : class
{
    protected Task ProcesarMensaje(
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken ct)
        => ProcesarMensaje(message, messageActions, evento => evento, ct);

    protected async Task ProcesarMensaje<TComando>(
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        Func<TEvento, TComando> crearComando,
        CancellationToken ct)
        where TComando : class
    {
        try
        {
            var evento = ServiceBusDeserializador.Deserializar<TEvento>(message.Body);
            await commandRouter.InvokeAsync(crearComando(evento), ct);
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
