using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventDriven.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.Programacion.SincronizarPlantillasCuandoLimitesDeJornadaActualizados;

public class FunctionEndpoint(IPrivateEventRouter privateEventRouter, ILogger<FunctionEndpoint> logger)
    : PrivateEventEndpointBase<LimitesDeJornadaActualizados>(privateEventRouter, logger)
{
    [Function("SincronizarPlantillasCuandoLimitesDeJornadaActualizados")]
    public async Task Run(
        [ServiceBusTrigger(
            topicName: "limites-de-jornada-actualizados",
            subscriptionName: "programacion-escucha-limites-de-jornada",
            Connection = "SERVICE_BUS_CONNECTION")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken ct)
        => await ProcesarMensaje(message, messageActions, ct);
}
