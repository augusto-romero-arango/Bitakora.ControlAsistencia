using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.Contracts.Programacion.Eventos;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction;

// HU-12: Primer ServiceBusTrigger del proyecto.
// CA-1: Funcion Azure que consume ProgramacionTurnoDiarioSolicitada desde Service Bus.
// CA-2: Deserializa el evento del mensaje y lo despacha al CommandHandler.
// ADR-0008: [Function("{Accion}Cuando{Evento}")]
public class FunctionEndpoint(ICommandRouter commandRouter, ILogger<FunctionEndpoint> logger)
    : ServiceBusEndpointBase<ProgramacionTurnoDiarioSolicitada>(commandRouter, logger)
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
