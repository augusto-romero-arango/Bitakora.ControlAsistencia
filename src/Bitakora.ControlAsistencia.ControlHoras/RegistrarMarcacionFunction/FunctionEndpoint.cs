using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction;

// HU-105: Endpoint HTTP POST para registrar marcaciones de entrada o salida
// CA-6: responde 202 Accepted tanto en creacion exitosa como en duplicado silencioso
// CA-7: Route: control-horas/marcaciones
// ADR-0008: [Function("RegistrarMarcacion")] como convencion de nombrado
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("RegistrarMarcacion")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "control-horas/marcaciones")]
        HttpRequest req,
        CancellationToken ct)
        => throw new NotImplementedException();
}
