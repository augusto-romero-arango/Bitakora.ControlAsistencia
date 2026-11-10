using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Cosmos.EventDriven.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.Sedes.ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado;

// Subscription propia de Sedes: ControlHoras tambien consume este topic (una por consumidor, MEF-ADR-0001).
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
