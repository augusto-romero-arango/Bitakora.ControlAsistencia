using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Cosmos.EventDriven.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoRegistroDeMarcacionCreado;

// Issue #270: reemplaza al FunctionEndpoint del folder AdicionarMarcacionCuandoMarcacionRegistrada
// (issue #213, retirado por este issue). El evento que cruza el ASB interno del BC ahora es
// RegistroDeMarcacionCreado (PrivateEvents.ControlHoras) -- MarcacionRegistrada dejo de implementar
// IPrivateEvent (CA-3). MEF-ADR-0024 decision #3: todo evento privado cruza fisicamente el ASB,
// aun intra-BC; decision #8: se despacha directo al IPrivateEventHandlerAsync via IPrivateEventRouter
// (PrivateEventEndpointBase) - sin comando espejo.
// MEF-ADR-0006: [Function("{Accion}Cuando{Evento}")], feature folder sin sufijo Function para
// triggers de ServiceBus.
// Topic "registro-de-marcacion-creado" + subscription "control-horas-escucha-registro-de-marcacion"
// (#274; reemplazan "marcacion-registrada" / "control-horas-escucha-marcacion" de #212, que quedan
// huerfanos y se retiran en un issue de infra aparte).
public class FunctionEndpoint(IPrivateEventRouter privateEventRouter, ILogger<FunctionEndpoint> logger)
    : PrivateEventEndpointBase<RegistroDeMarcacionCreado>(privateEventRouter, logger)
{
    [Function("AdicionarMarcacionCuandoRegistroDeMarcacionCreado")]
    public async Task Run(
        [ServiceBusTrigger(
            topicName: "registro-de-marcacion-creado",
            subscriptionName: "control-horas-escucha-registro-de-marcacion",
            Connection = "SERVICE_BUS_CONNECTION")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken ct)
        => await ProcesarMensaje(message, messageActions, ct);
}
