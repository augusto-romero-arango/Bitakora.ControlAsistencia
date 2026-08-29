using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Cosmos.EventDriven.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.Sedes.ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado;

// Suscripcion propia de Sedes sobre el topic que ControlHoras tambien consume -- una subscription
// por consumidor (MEF-ADR-0001). El despacho al IPrivateEventHandlerAsync lo hace la clase base via
// IPrivateEventRouter, sin comando espejo (MEF-ADR-0024 decision #8).
public class FunctionEndpoint(IPrivateEventRouter privateEventRouter, ILogger<FunctionEndpoint> logger)
    : PrivateEventEndpointBase<RegistroDeMarcacionCreado>(privateEventRouter, logger)
{
    [Function("ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado")]
    public async Task Run(
        [ServiceBusTrigger(
            topicName: "registro-de-marcacion-creado",
            subscriptionName: "sedes-escucha-registro-de-marcacion",
            Connection = "SERVICE_BUS_CONNECTION")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken ct)
        => await ProcesarMensaje(message, messageActions, ct);
}
