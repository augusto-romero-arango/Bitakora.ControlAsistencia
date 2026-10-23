using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.ActualizarControlDiario;

public class FunctionEndpoint(
    IPrivateEventRouter privateEventRouter,
    ICommandRouter commandRouter,
    ILogger<FunctionEndpoint> logger)
    : ServiceBusSessionEndpointBase(logger)
{
    [Function("ActualizarControlDiario")]
    public Task Run(
        [ServiceBusTrigger("actualizar-control-diario", Connection = "SERVICE_BUS_CONNECTION",
            IsSessionsEnabled = true)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken ct)
        => throw new NotImplementedException();

    protected override Func<CancellationToken, Task> Interpretar(ServiceBusReceivedMessage message)
        => throw new NotImplementedException();
}
