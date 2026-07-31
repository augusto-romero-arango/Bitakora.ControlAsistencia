using Azure.Messaging.ServiceBus;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Cosmos.EventDriven.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoRegistroDeMarcacionCreado;

// Issue #270: reemplaza al FunctionEndpoint del folder AdicionarMarcacionCuandoMarcacionRegistrada
// (issue #213, retirado por este issue). El evento que cruza el ASB interno del BC ahora es
// RegistroDeMarcacionCreado (PrivateEvents.ControlHoras) -- MarcacionRegistrada dejo de implementar
// IPrivateEvent (CA-3). ADR-0024 (marco) decision #3: todo evento privado cruza fisicamente el ASB,
// aun intra-BC; decision #8: se despacha directo al IPrivateEventHandlerAsync via IPrivateEventRouter
// (PrivateEventEndpointBase) - sin comando espejo.
// ADR-0008: [Function("{Accion}Cuando{Evento}")]
// Topic "registro-de-marcacion-creado" + subscription "control-horas-escucha-registro-de-marcacion"
// (#274; reemplazan "marcacion-registrada" / "control-horas-escucha-marcacion" de #212, que quedan
// huerfanos y se retiran en un issue de infra aparte).
public class FunctionEndpoint(IPrivateEventRouter privateEventRouter, ILogger<FunctionEndpoint> logger)
    : PrivateEventEndpointBase<RegistroDeMarcacionCreado>(privateEventRouter, logger)
{
    [Function("AdicionarMarcacionCuandoRegistroDeMarcacionCreado")]
    public Task Run(
        [ServiceBusTrigger(
            topicName: "registro-de-marcacion-creado",
            subscriptionName: "control-horas-escucha-registro-de-marcacion",
            Connection = "SERVICE_BUS_CONNECTION")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken ct)
        => throw new NotImplementedException();
}
