using System.Text.Json;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.ListarAusenciasDelEquipo;

// Function QUERY (MEF-ADR-0042) sobre la proyeccion AusenciaVigente (MEF-ADR-0035).
public class FunctionEndpoint(IDocumentStore store, ITenantContext tenantContext)
{
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

        var rango = RangoConsulta.Recortar(desde, hasta);
        var hastaAplicado = rango.HastaAplicado;

        await using var session = store.QuerySession(tenantContext.TenantId);
        var consulta = session.Query<AusenciaVigente>()
            .Where(a => a.PrimerDiaVigente <= hastaAplicado && a.UltimoDiaVigente >= desde);

        var codigos = filtro.Colaboradores?.ToList();
        if (codigos is { Count: > 0 })
            consulta = consulta.Where(a => codigos.Contains(a.CodigoColaborador));

        var vigentes = await consulta.ToListAsync(ct);

        var colaboradores = vigentes
            .Select(a => (Vista: a, Tramos: Recortar(a.TramosVigentes, desde, hastaAplicado)))
            .Where(x => x.Tramos.Count > 0)
            .GroupBy(x => x.Vista.CodigoColaborador)
            .Select(g => new AusenciasDeColaborador(
                g.Key,
                g.First().Vista.NombreCompleto,
                g.OrderBy(x => x.Tramos[0].Desde)
                    .Select(x => new AusenciaDelPeriodo(x.Vista.Id, x.Vista.Motivo, x.Tramos))
                    .ToList()))
            .OrderBy(c => c.NombreCompleto, StringComparer.Ordinal)
            .ThenBy(c => c.CodigoColaborador, StringComparer.Ordinal)
            .ToList();

        return new OkObjectResult(
            new ListaAusenciasDelEquipo(desde, hastaAplicado, rango.RangoRecortado, colaboradores));
    }

    private static List<TramoAplicado> Recortar(IReadOnlyList<TramoDeAusencia> tramos, DateOnly desde, DateOnly hasta) =>
        tramos
            .Where(t => t.Desde <= hasta && t.Hasta >= desde)
            .Select(t => new TramoAplicado(t.Desde > desde ? t.Desde : desde, t.Hasta < hasta ? t.Hasta : hasta))
            .ToList();
}
