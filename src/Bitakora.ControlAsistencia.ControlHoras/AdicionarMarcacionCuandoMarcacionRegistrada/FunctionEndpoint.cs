using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Cosmos.EventDriven.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoMarcacionRegistrada;

// issue #213: ServiceBusTrigger que consume MarcacionRegistrada desde el ASB interno del BC.
// ADR-0024 (marco) decision #3: todo evento privado cruza fisicamente el ASB, aun intra-BC;
// decision #8: se despacha directo al IPrivateEventHandlerAsync via IPrivateEventRouter
// (PrivateEventEndpointBase) - sin comando espejo. Reemplaza la entrega in-process previa
// (#209/#105) que no tenia [ServiceBusTrigger] ni FunctionEndpoint en este feature folder.
// ADR-0008: [Function("{Accion}Cuando{Evento}")]
// Topic "marcacion-registrada" + subscription "control-horas-escucha-marcacion" provisionados
// en #212 (infra/environments/dev/main.tf).
public class FunctionEndpoint(IPrivateEventRouter privateEventRouter, ILogger<FunctionEndpoint> logger)
    : PrivateEventEndpointBase<MarcacionRegistrada>(privateEventRouter, logger)
{
    [Function("AdicionarMarcacionCuandoMarcacionRegistrada")]
    public async Task Run(
        [ServiceBusTrigger(
            topicName: "marcacion-registrada",
            subscriptionName: "control-horas-escucha-marcacion",
            Connection = "SERVICE_BUS_CONNECTION")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken ct)
        => await ProcesarMensaje(message, messageActions, ct);
}
