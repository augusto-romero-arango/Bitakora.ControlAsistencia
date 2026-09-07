using Bitakora.ControlAsistencia.Programacion.ObtenerCuadroSemanalTurnos;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.ListarCuadrosSemanalesTurnos;

// Listado sin filtro server-side ni paginacion: el catalogo de plantillas es acotado y el cliente
// filtra (MEF-ADR-0042 seccion 1). Comparte el segmento "programacion/plantillas-semanales" con el
// POST de CrearPlantillaSemanal; cada uno declara su verbo (MEF-ADR-0006).
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ListarCuadrosSemanalesTurnos")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "programacion/plantillas-semanales")]
        HttpRequest req,
        CancellationToken ct)
    {
        // MEF-ADR-0028: la QuerySession se abre SIEMPRE acotada al tenant que resuelve
        // ITenantResolver -- nunca a un tenant id que llegara por query string.
        await using var session = store.QuerySession(tenantResolver.TenantId);

        // Orden estable (Nombre, Id) como contrato de la respuesta. Lista tambien las incompletas;
        // las retiradas no aparecen porque su cuadro se borro con la plantilla (ausencia = borrado,
        // no un flag que filtrar).
        var cuadros = await session.Query<CuadroSemanalTurnos>()
            .OrderBy(cuadro => cuadro.Nombre).ThenBy(cuadro => cuadro.Id)
            .ToListAsync(ct);

        // Una unica LoadManyAsync con la union de los TurnoId distintos de TODOS los cuadros, nunca
        // una por cuadro.
        var turnoIds = cuadros.SelectMany(cuadro => cuadro.Dias.Select(dia => dia.TurnoId))
            .Distinct()
            .ToList();
        var fichas = await session.LoadManyAsync<FichaTurno>(ct, turnoIds);
        var fichasPorId = fichas.ToDictionary(ficha => ficha.Id);

        return new OkObjectResult(cuadros
            .Select(cuadro => CuadroSemanalTurnosRespuesta.Componer(cuadro, fichasPorId))
            .ToList());
    }
}
