using Bitakora.ControlAsistencia.ReadModels.Programacion;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.ObtenerFichaTurno;

public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ObtenerFichaTurno")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "programacion/turnos/{id}")]
        HttpRequest req,
        string id,
        CancellationToken ct)
    {
        // MEF-ADR-0037 seccion 2: el {id} de ruta se parsea tipado una unica vez -- 400 explicito si
        // no es Guid -- y ToString() sin argumentos es la unica salida a string, porque el stream key
        // del catalogo es exactamente TurnoId.ToString() (StreamIdentity = AsString).
        if (!Guid.TryParse(id, out var turnoId))
            return new BadRequestObjectResult("El id del turno no es un Guid valido");

        // MEF-ADR-0028: la QuerySession se abre SIEMPRE acotada al tenant que resuelve
        // ITenantResolver -- nunca a un tenant id que llegara por ruta o query string.
        await using var session = store.QuerySession(tenantResolver.TenantId);
        var ficha = await session.LoadAsync<FichaTurno>(turnoId.ToString(), ct);

        return ficha is null ? new NotFoundResult() : new OkObjectResult(ficha);
    }
}
