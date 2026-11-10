using Bitakora.ControlAsistencia.ReadModels.Programacion;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.ObtenerFichaTurno;

public class FunctionEndpoint(IDocumentStore store, ITenantContext tenantContext)
{
    [Function("ObtenerFichaTurno")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "programacion/turnos/{id}")]
        HttpRequest req,
        string id,
        CancellationToken ct)
    {
        // ToString() sin argumentos es la unica salida a string: el stream key del catalogo es
        // exactamente TurnoId.ToString().
        if (!Guid.TryParse(id, out var turnoId))
            return new BadRequestObjectResult("El id del turno no es un Guid valido");

        // Siempre acotada al tenant de ITenantContext, nunca a un tenant id que llegue por ruta o query.
        await using var session = store.QuerySession(tenantContext.TenantId);
        var ficha = await session.LoadAsync<FichaTurno>(turnoId.ToString(), ct);

        return ficha is null ? new NotFoundResult() : new OkObjectResult(ficha);
    }
}
