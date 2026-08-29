using Bitakora.ControlAsistencia.ReadModels.Programacion;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.ListarFichasTurno;

// Listado sin filtro server-side ni paginacion: el catalogo es acotado (decenas por empresa) y el
// cliente filtra (MEF-ADR-0042 seccion 1). Comparte el segmento "programacion/turnos" con el POST
// de CrearTurno, que declara su propio verbo.
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ListarFichasTurno")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "programacion/turnos")]
        HttpRequest req,
        CancellationToken ct)
    {
        // MEF-ADR-0028: la QuerySession se abre SIEMPRE acotada al tenant que resuelve
        // ITenantResolver -- nunca a un tenant id que llegara por query string.
        await using var session = store.QuerySession(tenantResolver.TenantId);

        // Orden estable como contrato de la respuesta (CA-4): sin el, dos consultas consecutivas
        // podrian devolver el mismo catalogo permutado.
        var fichas = await session.Query<FichaTurno>()
            .OrderBy(f => f.Nombre).ThenBy(f => f.Id)
            .ToListAsync(ct);

        return new OkObjectResult(fichas);
    }
}
