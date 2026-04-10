using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.Contracts.Programacion.Eventos;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction;

// HU-12: Primer ServiceBusTrigger del proyecto.
// CA-1: Funcion Azure que consume ProgramacionTurnoDiarioSolicitada desde Service Bus.
// CA-2: Deserializa el evento del mensaje y lo despacha al CommandHandler.
// ADR-0008: [Function("{Accion}Cuando{Evento}")]
public class FunctionEndpoint(ICommandRouter commandRouter, ILogger<FunctionEndpoint> logger)
{
    [Function("AsignarTurnoCuandoProgramacionTurnoDiarioSolicitada")]
    public async Task Run(
        [ServiceBusTrigger(
            topicName: "programacion-turno-diario-solicitada",
            subscriptionName: "control-horas-escucha-programacion",
            Connection = "SERVICE_BUS_CONNECTION")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken ct)
    {
        try
        {
            var evento = ServiceBusDeserializador.Deserializar<ProgramacionTurnoDiarioSolicitada>(message.Body);
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
