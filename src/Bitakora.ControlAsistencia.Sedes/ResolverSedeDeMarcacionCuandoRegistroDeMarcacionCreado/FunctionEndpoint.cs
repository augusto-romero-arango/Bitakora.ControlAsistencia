using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Cosmos.EventDriven.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.Sedes.ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado;

// Issue #467: reaccion del dueno del dato (MEF-ADR-0046) sobre el mismo topic que consume
// ControlHoras (AdicionarMarcacionCuandoRegistroDeMarcacionCreado) -- subscription propia
// "sedes-escucha-registro-de-marcacion" (MEF-ADR-0001: una subscription por consumidor).
// MEF-ADR-0024 decision #8: despacho directo al IPrivateEventHandlerAsync via IPrivateEventRouter
// (PrivateEventEndpointBase), sin comando espejo.
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
