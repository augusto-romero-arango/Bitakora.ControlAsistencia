using System.Text.Json;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.ListarAusenciasDelEquipo;

public class FunctionEndpoint(IDocumentStore store, ITenantContext tenantContext)
{
    private const int TakeMaximo = 200;

    [Function("ListarAusenciasDelEquipo")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "query", Route = "programacion/ausencias")]
        HttpRequest req,
        CancellationToken ct)
    {
        if (!req.HasJsonContentType())
            return new ObjectResult("La query exige Content-Type: application/json")
            { StatusCode = StatusCodes.Status415UnsupportedMediaType };

        FiltroListarAusenciasDelEquipo? filtro;
        try
        {
            filtro = await req.ReadFromJsonAsync<FiltroListarAusenciasDelEquipo>(ct);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult("El body de la query no es un JSON valido");
        }

        if (filtro is null)
            return new BadRequestObjectResult("El body de la query es obligatorio");

        if (filtro.Desde is not { } desde || filtro.Hasta is not { } hasta)
            return new ObjectResult("Desde y Hasta son obligatorios")
            { StatusCode = StatusCodes.Status422UnprocessableEntity };

        if (desde > hasta)
            return new ObjectResult("Desde no puede ser posterior a Hasta")
            { StatusCode = StatusCodes.Status422UnprocessableEntity };

        if (filtro.Take is { } take && (take < 1 || take > TakeMaximo))
            return new ObjectResult($"Take debe ser un entero entre 1 y {TakeMaximo}")
            { StatusCode = StatusCodes.Status422UnprocessableEntity };

        CursorAusenciasEquipo? cursor = null;
        if (!string.IsNullOrEmpty(filtro.Cursor))
        {
            cursor = CursorAusenciasEquipo.Decodificar(filtro.Cursor);
            if (cursor is null)
                return new ObjectResult("El cursor no es valido")
                { StatusCode = StatusCodes.Status422UnprocessableEntity };
        }

        var rango = RangoConsulta.Recortar(desde, hasta);
        var hastaAplicado = rango.HastaAplicado;

        await using var session = store.QuerySession(tenantContext.TenantId);
        var consulta = session.Query<AusenciaVigente>()
            .Where(a => a.PrimerDiaVigente <= hastaAplicado && a.UltimoDiaVigente >= desde);

        var codigos = filtro.Colaboradores?.ToList();
        if (codigos is { Count: > 0 })
            consulta = consulta.Where(a => codigos.Contains(a.CodigoColaborador));

        var vigentes = await consulta.ToListAsync(ct);

        return new OkObjectResult(ListaAusenciasDelEquipo.Componer(desde, rango, vigentes, filtro.Take, cursor));
    }
}
