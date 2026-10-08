using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.SincronizarPlantillasCuandoDisenoDeTurnoActualizado;

public class FunctionEndpoint
{
    public Task Run(ServiceBusReceivedMessage message, ServiceBusMessageActions messageActions, CancellationToken ct)
        => throw new NotImplementedException();
}
