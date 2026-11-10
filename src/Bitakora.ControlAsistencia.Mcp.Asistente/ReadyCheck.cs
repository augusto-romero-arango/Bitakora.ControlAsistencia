using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Mcp.Asistente;

// El 200 significa solo "worker arriba": a diferencia del ReadyCheck de un dominio, este servidor no
// tiene persistencia propia y sus HttpClients tipados fallan en el arranque si falta una base URL.
public class ReadyCheck
{
    [Function("ready")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ready")]
        HttpRequest req) => new OkObjectResult("OK");
}
