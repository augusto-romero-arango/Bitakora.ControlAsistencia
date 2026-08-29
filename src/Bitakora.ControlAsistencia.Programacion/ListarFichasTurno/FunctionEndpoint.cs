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
//
// STUB de fase roja (projection-test-writer): Run lanza NotImplementedException a proposito. El
// comportamiento real -- session.QuerySession(tenantResolver.TenantId) acotada al tenant
// (MEF-ADR-0028), session.Query<FichaTurno>().OrderBy(f => f.Nombre).ThenBy(f => f.Id) (CA-4) -- es
// responsabilidad de projection-implementer. Este archivo solo fija la forma resoluble por DI que
// ComposicionServiciosTests.AgregarServiciosProgramacion_ResuelveElEndpointDeListarFichasTurno_...
// (Programacion.Tests) verifica.
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ListarFichasTurno")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "programacion/turnos")]
        HttpRequest req,
        CancellationToken ct) =>
        throw new NotImplementedException();
}
