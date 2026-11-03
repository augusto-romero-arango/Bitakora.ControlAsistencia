using System.Text.Json;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.ListarBorradoresDeProgramacion;

public class FunctionEndpoint(IDocumentStore store, ITenantContext tenantContext)
{
    private const int TakeMaximo = 200;

    [Function("ListarBorradoresDeProgramacion")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "query", Route = "programacion/borradores")]
        HttpRequest req,
        CancellationToken ct)
    {
        if (!req.HasJsonContentType())
            return new ObjectResult("La query exige Content-Type: application/json")
            { StatusCode = StatusCodes.Status415UnsupportedMediaType };

        FiltroListarBorradoresDeProgramacion? filtro;
        try
        {
            filtro = await req.ReadFromJsonAsync<FiltroListarBorradoresDeProgramacion>(ct);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult("El body de la query no es un JSON valido");
        }

        if (filtro is null)
            return new BadRequestObjectResult("El body de la query es obligatorio");

        if (filtro.FechaReferencia is not { } fechaReferencia)
            return new ObjectResult("FechaReferencia es obligatoria")
            { StatusCode = StatusCodes.Status422UnprocessableEntity };

        if (filtro.Desde is { } desde && filtro.Hasta is { } hasta && desde > hasta)
            return new ObjectResult("Desde no puede ser posterior a Hasta")
            { StatusCode = StatusCodes.Status422UnprocessableEntity };

        if (filtro.Take is { } take && (take < 1 || take > TakeMaximo))
            return new ObjectResult($"Take debe ser un entero entre 1 y {TakeMaximo}")
            { StatusCode = StatusCodes.Status422UnprocessableEntity };

        CursorBorradoresDeProgramacion? cursor = null;
        if (!string.IsNullOrEmpty(filtro.Cursor))
        {
            cursor = CursorBorradoresDeProgramacion.Decodificar(filtro.Cursor);
            if (cursor is null)
                return new ObjectResult("El cursor no es valido")
                { StatusCode = StatusCodes.Status422UnprocessableEntity };
        }

        await using var session = store.QuerySession(tenantContext.TenantId);
        var consulta = session.Query<BorradorDeProgramacion>().AsQueryable();

        var codigos = filtro.Colaboradores?.ToList();
        if (codigos is { Count: > 0 })
            consulta = consulta.Where(b => codigos.Contains(b.CodigoColaborador));

        var borradores = await consulta.ToListAsync(ct);

        return new OkObjectResult(ListaBorradoresDeProgramacion.Componer(
            fechaReferencia, filtro.Desde, filtro.Hasta, borradores, filtro.Take, cursor));
    }
}
