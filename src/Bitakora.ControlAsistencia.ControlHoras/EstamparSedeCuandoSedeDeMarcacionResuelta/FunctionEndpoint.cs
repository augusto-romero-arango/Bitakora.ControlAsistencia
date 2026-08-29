using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.PrivateEvents.Sedes;
using Cosmos.EventDriven.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.EstamparSedeCuandoSedeDeMarcacionResuelta;

// Issue #463: cierre del enriquecimiento coreografiado (MEF-ADR-0046). SedeDeMarcacionResuelta
// cruza fisicamente el ASB interno del BC (topic "sede-de-marcacion-resuelta", creado por #467).
// MEF-ADR-0024 decision #3 + #8: se despacha directo al IPrivateEventHandlerAsync via
// IPrivateEventRouter (PrivateEventEndpointBase) -- sin comando espejo.
// MEF-ADR-0006: [Function("{Accion}Cuando{Evento}")], feature folder sin sufijo Function para
// triggers de ServiceBus.
// La suscripcion nace session-enabled (fan-in dentro del topic, MEF-ADR-0026): el productor
// (Sedes) publica con PublishOptions.GroupId = CodigoColaborador (#467).
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
