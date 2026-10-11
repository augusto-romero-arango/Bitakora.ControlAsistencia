using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarDiasSinProgramar;

public class FunctionEndpoint(IDocumentStore store, ITenantContext tenantContext)
{
    [Function("ListarDiasSinProgramar")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "query", Route = "control-horas/dias-sin-programar")]
        HttpRequest req,
        CancellationToken ct) => throw new NotImplementedException();
}
