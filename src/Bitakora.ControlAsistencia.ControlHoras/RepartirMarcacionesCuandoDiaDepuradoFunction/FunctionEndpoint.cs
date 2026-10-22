using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.RepartirMarcacionesCuandoDiaDepuradoFunction;

// Comando espejo deliberado (excepcion a MEF-ADR-0024 decision #8): DiaDepurado ya tiene un
// IPrivateEventHandlerAsync (RecibirDepuracionCuandoDiaDepurado), y Wolverine ejecutaria juntos dos
// handlers del mismo tipo en cada invocacion.
// El nombre de la suscripcion debe coincidir con el declarado en infra/environments/dev/main.tf.
public class FunctionEndpoint(ICommandRouter commandRouter, ILogger<FunctionEndpoint> logger)
    : ServiceBusEndpointBase<DiaDepurado>(commandRouter, logger)
{
    [Function("RepartirMarcacionesCuandoDiaDepurado")]
    public async Task Run(
        [ServiceBusTrigger(
            topicName: "dia-depurado",
            subscriptionName: "control-horas-reparte-marcaciones",
            Connection = "SERVICE_BUS_CONNECTION")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken ct)
        => await ProcesarMensaje(message, messageActions,
            dia => new RepartirMarcaciones(dia.CodigoColaborador, dia.Fecha), ct);
}
