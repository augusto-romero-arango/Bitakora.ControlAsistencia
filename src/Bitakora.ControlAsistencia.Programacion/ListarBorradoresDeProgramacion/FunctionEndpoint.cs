using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.ListarBorradoresDeProgramacion;

public class FunctionEndpoint(IDocumentStore store, ITenantContext tenantContext)
{
    [Function("ListarBorradoresDeProgramacion")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "query", Route = "programacion/borradores")]
        HttpRequest req,
        CancellationToken ct)
    {
        _ = (store, tenantContext, req, ct);
        await Task.CompletedTask;
        throw new NotImplementedException();
    }
}
