using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventDriven.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.Programacion.SincronizarPlantillasCuandoDisenoDeTurnoActualizado;

// Dos cambios rapidos sobre una misma plantilla escriben concurrentemente su stream; el reintento por
// concurrencia optimista de Wolverine lo resuelve (fan-out, MEF-ADR-0026) y la version por turno
// descarta el desorden.
public class FunctionEndpoint(IPrivateEventRouter privateEventRouter, ILogger<FunctionEndpoint> logger)
    : PrivateEventEndpointBase<DisenoDeTurnoActualizado>(privateEventRouter, logger)
{
    [Function("SincronizarPlantillasCuandoDisenoDeTurnoActualizado")]
    public async Task Run(
        [ServiceBusTrigger(
            topicName: "diseno-de-turno-actualizado",
            subscriptionName: "programacion-escucha-diseno-de-turno",
            Connection = "SERVICE_BUS_CONNECTION")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken ct)
        => await ProcesarMensaje(message, messageActions, ct);
}
