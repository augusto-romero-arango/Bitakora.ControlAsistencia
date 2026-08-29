using Bitakora.ControlAsistencia.ReadModels.Programacion;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.ObtenerFichaTurno;

// Issue #496 (creacion): Function GET del read model FichaTurno (via (a) proyeccion materializada,
// skills/projections/naming.md, MEF-ADR-0006). Feature folder sin sufijo Function, un namespace por
// query (skills/projections/read-apis.md). Mismo par (IDocumentStore, ITenantResolver) que
// ObtenerFichaSede -- precedente exacto del issue #461.
//
// CA-3: consulta puntual por {id} de ruta -- 404 sin body cuando la ficha no existe.
//
// MEF-ADR-0037 seccion 2: el {id} de ruta nace Guid (TurnoId) -- se parsea tipado una unica vez
// antes de tocar LoadAsync, con 400 explicito si no es un Guid valido, y ToString() sin argumentos
// como unica salida a string: el stream key del catalogo es exactamente evento.TurnoId.ToString()
// (Events.StreamIdentity = AsString, documentado en FichaTurnoProjection), y FichaTurno.Id -- el
// TId del read model N1 -- es ese mismo string.
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ObtenerFichaTurno")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "programacion/turnos/{id}")]
        HttpRequest req,
        string id,
        CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var turnoId))
            return new BadRequestObjectResult("El id del turno no es un Guid valido");

        // CA-3/MEF-ADR-0028: la QuerySession se abre SIEMPRE acotada al tenant que resuelve
        // ITenantResolver -- nunca a un tenant id que llegara por ruta o query string.
        await using var session = store.QuerySession(tenantResolver.TenantId);
        var ficha = await session.LoadAsync<FichaTurno>(turnoId.ToString(), ct);

        return ficha is null ? new NotFoundResult() : new OkObjectResult(ficha);
    }
}
