using Bitakora.ControlAsistencia.ReadModels.Sedes;
using Bitakora.ControlAsistencia.Sedes.Entities;
using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Sedes.ObtenerFichaSede;

// El stream key se construye solo con SedeAggregateRoot.ComputarStreamId, nunca concatenando aqui.
public class FunctionEndpoint(IDocumentStore store, ITenantContext tenantContext)
{
    [Function("ObtenerFichaSede")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sedes/fichas/{codigo}")]
        HttpRequest req,
        string codigo,
        CancellationToken ct)
    {
        if (!CodigoSedeDeRuta.EsValido(codigo, out var errorDeCodigo))
            return errorDeCodigo;

        var streamKey = SedeAggregateRoot.ComputarStreamId(codigo);

        // Tenant de ITenantContext, nunca uno del request (MEF-ADR-0028).
        await using var session = store.QuerySession(tenantContext.TenantId);
        var ficha = await session.LoadAsync<FichaSede>(streamKey, ct);

        return ficha is null ? new NotFoundResult() : new OkObjectResult(ficha);
    }
}
