using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.ListarAusenciasDelEquipo;

// Function QUERY (MEF-ADR-0042) sobre la proyeccion AusenciaVigente (MEF-ADR-0035).
public class FunctionEndpoint(IDocumentStore store, ITenantContext tenantContext)
{
    [Function("ListarAusenciasDelEquipo")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "query", Route = "programacion/ausencias")]
        HttpRequest req,
        CancellationToken ct)
        => throw new NotImplementedException();
}
