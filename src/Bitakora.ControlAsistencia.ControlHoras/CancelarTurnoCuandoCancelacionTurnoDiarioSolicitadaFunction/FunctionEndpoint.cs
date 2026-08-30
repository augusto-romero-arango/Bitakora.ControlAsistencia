using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Cosmos.EventDriven.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.CancelarTurnoCuandoCancelacionTurnoDiarioSolicitadaFunction;

// Issue #499: ServiceBusTrigger que consume CancelacionTurnoDiarioSolicitada desde el ASB interno
// del BC (topic creado en #498). MEF-ADR-0024 decision #8: sin comando espejo, se despacha directo
// al IPrivateEventHandlerAsync via IPrivateEventRouter (PrivateEventEndpointBase).
// Nombre de subscription y topologia final a juicio del implementer/infra-writer (MEF-ADR-0026:
// riesgo declarado en #498 -- este handler y AsignarTurno... escriben sobre los mismos streams cd:).
public class FunctionEndpoint(IPrivateEventRouter privateEventRouter, ILogger<FunctionEndpoint> logger)
    : PrivateEventEndpointBase<CancelacionTurnoDiarioSolicitada>(privateEventRouter, logger)
{
    [Function("CancelarTurnoCuandoCancelacionTurnoDiarioSolicitada")]
    public async Task Run(
        [ServiceBusTrigger(
            topicName: "cancelacion-turno-diario-solicitada",
            subscriptionName: "control-horas-escucha-cancelacion",
            Connection = "SERVICE_BUS_CONNECTION")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken ct)
        => await ProcesarMensaje(message, messageActions, ct);
}
