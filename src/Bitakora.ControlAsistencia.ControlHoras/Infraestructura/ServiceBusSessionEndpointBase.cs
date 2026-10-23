using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.Infraestructura;

/// <summary>
/// Base de los endpoints de fan-in con sesion (MEF-ADR-0026). Separa dos etapas con liquidacion distinta:
/// interpretar el mensaje (fallo = dead letter, reintentar no lo arregla) e invocar el router
/// (fallo = abandono para reintento; lock perdido = solo warning).
/// </summary>
public abstract class ServiceBusSessionEndpointBase(ILogger logger)
{
    protected async Task ProcesarMensajeDeSesion(
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken ct)
    {
        Func<CancellationToken, Task> invocacion;
        try
        {
            invocacion = Interpretar(message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Mensaje ilegible {MessageId} (SessionId {SessionId}, DeliveryCount {DeliveryCount}) - se envia a dead letter",
                message.MessageId, message.SessionId, message.DeliveryCount);
            await messageActions.DeadLetterMessageAsync(message, cancellationToken: ct);
            return;
        }

        try
        {
            await invocacion(ct);
            await messageActions.CompleteMessageAsync(message, ct);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessageLockLost)
        {
            logger.LogWarning(ex,
                "Lock perdido para mensaje {MessageId} (SessionId {SessionId}, DeliveryCount {DeliveryCount}) - Service Bus lo re-entregara automaticamente",
                message.MessageId, message.SessionId, message.DeliveryCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error procesando mensaje {MessageId} (SessionId {SessionId}, DeliveryCount {DeliveryCount}) - se abandona para reintento",
                message.MessageId, message.SessionId, message.DeliveryCount);
            await messageActions.AbandonMessageAsync(message, cancellationToken: ct);
        }
    }

    protected abstract Func<CancellationToken, Task> Interpretar(ServiceBusReceivedMessage message);
}
