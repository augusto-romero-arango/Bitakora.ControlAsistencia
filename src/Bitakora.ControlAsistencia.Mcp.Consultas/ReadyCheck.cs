using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Mcp.Consultas;

// El 200 significa solo "worker arriba". A diferencia de los ReadyCheck de los dominios -- que
// abren una conexion contra el event store porque ese write-path frio fue la causa raiz del
// incidente del issue #399 -- este app no tiene event store ni recurso propio que calentar: es un
// cliente HTTP puro de los Function Apps del BC (CA-ADR-0029) y sus HttpClients tipados fallan en
// el arranque si falta una base URL, no en la primera peticion. No copiar aqui la sonda de los
// dominios: no hay nada que sondear.
public class ReadyCheck
{
    [Function("ready")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ready")]
        HttpRequest req) => new OkObjectResult("OK");
}
