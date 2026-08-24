using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Cosmos.EventDriven.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.RecibirDepuracionCuandoDiaDepurado;

// MEF-ADR-0024 decision #3: DiaDepurado cruza fisicamente el ASB interno del BC aun siendo
// consumido dentro del mismo BC; decision #8: se despacha directo al IPrivateEventHandlerAsync via
// IPrivateEventRouter, sin comando espejo.
// El nombre de la suscripcion debe coincidir con el declarado en infra/environments/dev/main.tf.
public class FunctionEndpoint(IPrivateEventRouter privateEventRouter, ILogger<FunctionEndpoint> logger)
    : PrivateEventEndpointBase<DiaDepurado>(privateEventRouter, logger)
{
    [Function("RecibirDepuracionCuandoDiaDepurado")]
    public async Task Run(
        [ServiceBusTrigger(
            topicName: "dia-depurado",
            subscriptionName: "control-horas-escucha-dia-depurado",
            Connection = "SERVICE_BUS_CONNECTION")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken ct)
        => await ProcesarMensaje(message, messageActions, ct);
}
