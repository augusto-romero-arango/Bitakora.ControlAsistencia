using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Cosmos.EventDriven.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.CancelarAusenciaCuandoAusenciaDiariaCancelada;

public class FunctionEndpoint(IPrivateEventRouter privateEventRouter, ILogger<FunctionEndpoint> logger)
    : PrivateEventEndpointBase<AusenciaDiariaCancelada>(privateEventRouter, logger)
{
    [Function("CancelarAusenciaCuandoAusenciaDiariaCancelada")]
    public async Task Run(
        [ServiceBusTrigger(
            topicName: "ausencia-diaria-cancelada",
            subscriptionName: "control-horas-escucha-ausencia-cancelada",
            Connection = "SERVICE_BUS_CONNECTION")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken ct)
        => await ProcesarMensaje(message, messageActions, ct);
}
