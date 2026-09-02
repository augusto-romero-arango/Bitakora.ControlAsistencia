using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Mcp.Comandos;

// El 200 significa solo "worker arriba" (MEF-ADR-0048 seccion 3): a diferencia del ReadyCheck de
// un dominio, que abre una conexion contra el event store, este servidor no tiene persistencia
// propia -- es cliente HTTP puro (MEF-ADR-0047 decision 3) y sus HttpClients tipados fallan en el
// arranque si falta una base URL, no en la primera peticion.
public class ReadyCheck
{
    [Function("ready")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ready")]
        HttpRequest req) => new OkObjectResult("OK");
}
