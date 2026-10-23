using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.Infraestructura;

public abstract class ServiceBusSessionEndpointBase(ILogger logger)
{
    protected Task ProcesarMensajeDeSesion(
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken ct)
        => throw new NotImplementedException();

    protected abstract Func<CancellationToken, Task> Interpretar(ServiceBusReceivedMessage message);
}
