using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.ListarLimitesDeJornada;

public class FunctionEndpoint(IDocumentStore store, ITenantContext tenantContext)
{
    [Function("ListarLimitesDeJornada")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "programacion/jornadas")]
        HttpRequest req,
        CancellationToken ct) =>
        throw new NotImplementedException();
}
