using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Cosmos.EventDriven.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.CancelarTurnoCuandoCancelacionTurnoDiarioSolicitada;

// MEF-ADR-0024 decision #8: sin comando espejo, se despacha directo al IPrivateEventHandlerAsync
// via IPrivateEventRouter (PrivateEventEndpointBase).
// MEF-ADR-0026: esta subscription y control-horas-escucha-programacion escriben sobre los mismos
// streams cd: sin serializacion por clave -- dos mensajes concurrentes del mismo colaborador+fecha
// pueden colisionar en el append.
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
