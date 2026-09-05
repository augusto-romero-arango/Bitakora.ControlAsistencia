using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.ListarCuadrosSemanalesTurnos;

// Issue #625: GET sin filtro server-side ni paginacion (MEF-ADR-0042 seccion 1, catalogo acotado de
// plantillas). Lista tambien las incompletas; no lista las retiradas (su cuadro se borro). Comparte
// segmento "programacion/plantillas-semanales" con el POST de #620 -- cada uno declara su verbo
// (MEF-ADR-0006). Reusa CuadroSemanalTurnosRespuesta del namespace hermano ObtenerCuadroSemanalTurnos.
//
// Stub de fase roja (projection-test-writer): la implementacion real -- cargar todos los cuadros,
// una unica LoadManyAsync<FichaTurno> con la union de TurnoId, y componer cada uno -- es
// responsabilidad del projection-implementer.
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ListarCuadrosSemanalesTurnos")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "programacion/plantillas-semanales")]
        HttpRequest req,
        CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
