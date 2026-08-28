using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Sedes;

public partial class ReadyCheck(IEventStoreReadinessProbe probe)
{
    [Function("ready")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ready")]
        HttpRequest req,
        CancellationToken ct)
    {
        try
        {
            await probe.VerificarAsync(ct);
        }
        catch (Exception ex)
        {
            return new ObjectResult($"{Mensajes.EventStoreNoDisponible}: {ex.Message}")
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable
            };
        }

        return new OkObjectResult("OK");
    }
}
