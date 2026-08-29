using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.PrivateEvents.Sedes;
using Cosmos.EventDriven.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.EstamparSedeCuandoSedeDeMarcacionResuelta;

// IsSessionsEnabled va atado a la infra: la suscripcion se declara session-enabled en Terraform y
// el productor (Sedes) publica con GroupId = CodigoColaborador. Las tres piezas van juntas -- sin
// sesion, dos resoluciones del mismo colaborador escriben concurrentemente el mismo cd:
// (MEF-ADR-0026); sin GroupId, el mensaje se dead-lettera en la suscripcion session-enabled.
public class FunctionEndpoint(IPrivateEventRouter privateEventRouter, ILogger<FunctionEndpoint> logger)
    : PrivateEventEndpointBase<SedeDeMarcacionResuelta>(privateEventRouter, logger)
{
    [Function("EstamparSedeCuandoSedeDeMarcacionResuelta")]
    public async Task Run(
        [ServiceBusTrigger(
            topicName: "sede-de-marcacion-resuelta",
            subscriptionName: "control-horas-escucha-sede-de-marcacion-resuelta",
            Connection = "SERVICE_BUS_CONNECTION",
            IsSessionsEnabled = true)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken ct)
        => await ProcesarMensaje(message, messageActions, ct);
}
