using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.RepartirMarcacionesCuandoDiaDepuradoFunction;

public class FunctionEndpoint(ICommandRouter commandRouter, ILogger<FunctionEndpoint> logger)
{
    [Function("RepartirMarcacionesCuandoDiaDepurado")]
    public async Task Run(
        [ServiceBusTrigger(
            topicName: "dia-depurado",
            subscriptionName: "control-horas-reparte-marcaciones",
            Connection = "SERVICE_BUS_CONNECTION")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken ct)
    {
        try
        {
            var dia = ServiceBusDeserializador.Deserializar<DiaDepurado>(message.Body);
            await commandRouter.InvokeAsync(new RepartirMarcaciones(dia.CodigoColaborador, dia.Fecha), ct);
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
