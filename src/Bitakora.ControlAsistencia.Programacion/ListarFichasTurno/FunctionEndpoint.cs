using Bitakora.ControlAsistencia.ReadModels.Programacion;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.ListarFichasTurno;

// Issue #496 (creacion): Function GET de listado sobre FichaTurno -- mismo segmento de recurso que
// CrearTurno ("programacion/turnos"), sin ningun filtro server-side (catalogo acotado, decenas por
// empresa) ni paginacion, con orden estable por Nombre (desempate por Id) como contrato de la
// respuesta (MEF-ADR-0042 seccion 1, CA-4). Mismo par (IDocumentStore, ITenantResolver) que
// ListarFichasSede -- precedente exacto del issue #461.
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ListarFichasTurno")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "programacion/turnos")]
        HttpRequest req,
        CancellationToken ct)
    {
        // CA-4/MEF-ADR-0028: la QuerySession se abre SIEMPRE acotada al tenant que resuelve
        // ITenantResolver -- nunca a un tenant id que llegara por query string.
        await using var session = store.QuerySession(tenantResolver.TenantId);

        // CA-4: sin filtro ni paginacion, orden estable por Nombre (desempate por Id).
        var fichas = await session.Query<FichaTurno>()
            .OrderBy(f => f.Nombre).ThenBy(f => f.Id)
            .ToListAsync(ct);

        return new OkObjectResult(fichas);
    }
}
