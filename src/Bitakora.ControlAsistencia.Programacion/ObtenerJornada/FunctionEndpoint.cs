using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.ObtenerJornada;

public class FunctionEndpoint(IDocumentStore store, ITenantContext tenantContext)
{
    [Function("ObtenerJornada")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "programacion/jornadas/{id}")]
        HttpRequest req,
        string id,
        CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var jornadaId))
            return new BadRequestObjectResult("El id de la jornada no es un Guid valido");

        await using var session = store.QuerySession(tenantContext.TenantId);
        var jornada = await session.Events.AggregateStreamAsync<Jornada>(jornadaId.ToString(), token: ct);

        return jornada is null ? new NotFoundResult() : new OkObjectResult(jornada.Describir());
    }
}
