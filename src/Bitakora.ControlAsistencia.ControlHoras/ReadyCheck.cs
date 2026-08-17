using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.ControlHoras;

public partial class ReadyCheck(IEventStoreReadinessProbe probe)
{
    [Function("ready")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ready")]
        HttpRequest req,
        CancellationToken ct) => throw new NotImplementedException();
}
