using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventDriven.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.Programacion.SincronizarPlantillasCuandoDisenoDeTurnoActualizado;

// Fan-out sin sesion (MEF-ADR-0026): dos cambios rapidos sobre una misma plantilla pueden chocar por
// concurrencia optimista; el perdedor termina en la DLQ (fallo visible) y reprocesarlo es idempotente
// porque la version por turno descarta lo ya aplicado.
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
