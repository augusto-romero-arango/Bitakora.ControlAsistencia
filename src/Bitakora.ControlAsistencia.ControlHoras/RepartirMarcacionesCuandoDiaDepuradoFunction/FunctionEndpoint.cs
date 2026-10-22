using Azure.Messaging.ServiceBus;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.RepartirMarcacionesCuandoDiaDepuradoFunction;

public class FunctionEndpoint(ICommandRouter commandRouter, ILogger<FunctionEndpoint> logger)
{
    [Function("RepartirMarcacionesCuandoDiaDepurado")]
    public Task Run(
        [ServiceBusTrigger(
            topicName: "dia-depurado",
            subscriptionName: "control-horas-reparte-marcaciones",
            Connection = "SERVICE_BUS_CONNECTION")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken ct)
        => throw new NotImplementedException();
}
