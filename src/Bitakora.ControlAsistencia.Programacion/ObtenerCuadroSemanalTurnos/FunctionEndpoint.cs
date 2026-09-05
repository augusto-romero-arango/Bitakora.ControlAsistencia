using Bitakora.ControlAsistencia.ReadModels.Programacion;
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
        // MEF-ADR-0037 seccion 2: el {id} de ruta se parsea tipado una unica vez -- 400 explicito si
        // no es Guid -- y ToString() sin argumentos es la unica salida a string: el stream key de la
        // plantilla es exactamente PlantillaId.ToString() (StreamIdentity = AsString).
        if (!Guid.TryParse(id, out var plantillaId))
            return new BadRequestObjectResult("El id de la plantilla no es un Guid valido");

        // MEF-ADR-0028: la QuerySession se abre SIEMPRE acotada al tenant que resuelve
        // ITenantResolver -- nunca a un tenant id que llegara por ruta o query string.
        await using var session = store.QuerySession(tenantResolver.TenantId);
        var cuadro = await session.LoadAsync<CuadroSemanalTurnos>(plantillaId.ToString(), ct);

        if (cuadro is null)
            return new NotFoundResult();

        // Composicion en lectura (opcion B, CA-ADR-0034 decision 5 enmendada): una unica
        // LoadManyAsync con la union de los TurnoId distintos del cuadro, nunca una por dia.
        var turnoIds = cuadro.Dias.Select(dia => dia.TurnoId).Distinct().ToList();
        var fichas = await session.LoadManyAsync<FichaTurno>(ct, turnoIds);
        var fichasPorId = fichas.ToDictionary(ficha => ficha.Id);

        return new OkObjectResult(CuadroSemanalTurnosRespuesta.Componer(cuadro, fichasPorId));
    }
}
