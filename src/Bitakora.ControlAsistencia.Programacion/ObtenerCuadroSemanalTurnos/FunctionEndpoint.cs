using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.ObtenerCuadroSemanalTurnos;

// Issue #625: GET del cuadro semanal RESUELTO (composicion en lectura con FichaTurno, opcion B).
// Mismo segmento de recurso que el POST de #620 y el DELETE de #623 (programacion/plantillas-semanales/{id}),
// leido desde el read-side -- criterio de ObtenerFichaTurno sobre programacion/turnos.
//
// Stub de fase roja (projection-test-writer): la implementacion real -- Guid.TryParse del {id} con
// 400 explicito (MEF-ADR-0037 seccion 2), LoadAsync<CuadroSemanalTurnos>, LoadManyAsync<FichaTurno>
// de la union de TurnoId, y CuadroSemanalTurnosRespuesta.Componer -- es responsabilidad del
// projection-implementer.
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ObtenerCuadroSemanalTurnos")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "programacion/plantillas-semanales/{id}")]
        HttpRequest req,
        string id,
        CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
