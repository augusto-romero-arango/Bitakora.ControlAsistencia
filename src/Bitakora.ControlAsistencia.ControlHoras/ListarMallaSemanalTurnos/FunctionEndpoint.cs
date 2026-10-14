using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarMallaSemanalTurnos;

public class FunctionEndpoint(IDocumentStore store, ITenantContext tenantContext)
{
    [Function("ListarMallaSemanalTurnos")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "query", Route = "control-horas/malla-semanal-turnos")]
        HttpRequest req,
        CancellationToken ct) => throw new NotImplementedException();
}
