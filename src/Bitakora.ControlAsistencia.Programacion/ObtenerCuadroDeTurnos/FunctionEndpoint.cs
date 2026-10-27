using Bitakora.ControlAsistencia.ReadModels.Programacion;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.ObtenerCuadroDeTurnos;

public class FunctionEndpoint(IDocumentStore store, ITenantContext tenantContext)
{
    [Function("ObtenerCuadroDeTurnos")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "programacion/plantillas-turnos/{id}")]
        HttpRequest req,
        string id,
        CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var plantillaId))
            return new BadRequestObjectResult("El id de la plantilla no es un Guid valido");

        await using var session = store.QuerySession(tenantContext.TenantId);
        var cuadro = await session.LoadAsync<CuadroDeTurnos>(plantillaId.ToString(), ct);

        if (cuadro is null)
            return new NotFoundResult();

        return new OkObjectResult(CuadroDeTurnosRespuesta.Componer(cuadro));
    }
}
