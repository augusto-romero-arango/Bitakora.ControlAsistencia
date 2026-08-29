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
// STUB de fase roja (projection-test-writer): Run lanza NotImplementedException a proposito. El
// comportamiento real -- parsear {id} como Guid (MEF-ADR-0037 seccion 2, el TurnoId nace Guid),
// session.QuerySession(tenantResolver.TenantId) acotada al tenant (MEF-ADR-0028), session.
// LoadAsync<FichaTurno>(turnoId.ToString()) y el 200/404 (CA-3) -- es responsabilidad de
// projection-implementer. Este archivo solo fija la forma resoluble por DI que
// ComposicionServiciosTests.AgregarServiciosProgramacion_ResuelveElEndpointDeObtenerFichaTurno_...
// (Programacion.Tests) verifica.
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ObtenerFichaTurno")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "programacion/turnos/{id}")]
        HttpRequest req,
        string id,
        CancellationToken ct) =>
        throw new NotImplementedException();
}
