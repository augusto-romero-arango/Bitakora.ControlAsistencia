using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Cosmos.EventDriven.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction;

// HU-12: ServiceBusTrigger que consume ProgramacionTurnoDiarioSolicitada desde el ASB interno del BC.
// issue #210 / ADR-0024 (decision #8): el evento es IPrivateEvent intra-BC y el comando equivalente
//   seria un espejo, asi que se despacha directo al IPrivateEventHandlerAsync via IPrivateEventRouter
//   (PrivateEventEndpointBase) - sin comando espejo.
// CA-3: el [Function] y el [ServiceBusTrigger] sobre topic/subscription se conservan.
// ADR-0008: [Function("{Accion}Cuando{Evento}")]
public class FunctionEndpoint(IPrivateEventRouter privateEventRouter, ILogger<FunctionEndpoint> logger)
    : PrivateEventEndpointBase<ProgramacionTurnoDiarioSolicitada>(privateEventRouter, logger)
{
    [Function("AsignarTurnoCuandoProgramacionTurnoDiarioSolicitada")]
    public async Task Run(
        [ServiceBusTrigger(
            topicName: "programacion-turno-diario-solicitada",
            subscriptionName: "control-horas-escucha-programacion",
            Connection = "SERVICE_BUS_CONNECTION")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken ct)
        => await ProcesarMensaje(message, messageActions, ct);
}
