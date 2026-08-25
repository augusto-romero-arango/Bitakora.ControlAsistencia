using System.Text.Json;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarTurnosVigentes;

// Function QUERY (RFC 10008, MEF-ADR-0042) sobre la vista materializada TurnoVigente, via (a') de
// MEF-ADR-0035: el rango de fechas obligatorio es un filtro estructurado. El nombre de la Function
// y su Route son los mismos que tenia sobre GET -- cruzar la frontera de verbo no los cambia
// (MEF-ADR-0042 seccion 5).
//
// A diferencia del filtro homonimo de ListarAsistenciasDiarias, CodigoColaborador y SedeId son
// OPCIONALES: su ausencia es el panorama de todos los colaboradores y la ausencia de filtro por
// sede, nunca un 422.
//
// SedeId filtra "dias donde AL MENOS un bloque rige en esa sede": la sede va por bloque, nunca por
// dia, por eso el predicado es Bloques.Any(...) y no un campo de TurnoVigente. Marten traduce esa
// igualdad dentro de una coleccion hija a containment JSONB -- data -> 'Bloques' @> '[{"SedeId":
// ...}]' --, la unica forma elegible para indice GIN. Esa misma semantica excluye sin rama
// explicita los bloques que no traen la clave SedeId.
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ListarTurnosVigentes")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "query", Route = "control-horas/turnos-vigentes")]
        HttpRequest req,
        CancellationToken ct)
    {
        // El 415 va ANTES de leer el body: ante un Content-Type no-JSON, ReadFromJsonAsync lanza
        // una excepcion que NO es JsonException y escaparia como 500 pese al catch de abajo.
        if (!req.HasJsonContentType())
            return new ObjectResult("La query exige Content-Type: application/json")
            { StatusCode = StatusCodes.Status415UnsupportedMediaType };

        FiltroListarTurnosVigentes? filtro;
        try
        {
            filtro = await req.ReadFromJsonAsync<FiltroListarTurnosVigentes>(ct);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult("El body de la query no es un JSON valido");
        }

        if (filtro is null)
            return new BadRequestObjectResult("El body de la query es obligatorio");

        if (filtro.DesdeFecha is null || filtro.HastaFecha is null)
            return new ObjectResult("DesdeFecha y HastaFecha son obligatorios")
            { StatusCode = StatusCodes.Status422UnprocessableEntity };

        if (filtro.DesdeFecha > filtro.HastaFecha)
            return new ObjectResult("DesdeFecha no puede ser posterior a HastaFecha")
            { StatusCode = StatusCodes.Status422UnprocessableEntity };

        var desde = filtro.DesdeFecha.Value;
        var codigoColaborador = filtro.CodigoColaborador;
        var sedeId = filtro.SedeId;
        var rangoAplicado = RangoConsulta.Recortar(desde, filtro.HastaFecha.Value);

        // Sesion acotada al tenant que resuelve ITenantResolver, nunca a un dato de la request
        // (mitigacion estructural contra BOLA/IDOR, MEF-ADR-0028).
        await using var session = store.QuerySession(tenantResolver.TenantId);

        IQueryable<TurnoVigente> query = session.Query<TurnoVigente>()
            .Where(v => v.Fecha >= desde && v.Fecha <= rangoAplicado.HastaAplicado);

        if (!string.IsNullOrEmpty(codigoColaborador))
            query = query.Where(v => v.CodigoColaborador == codigoColaborador);

        if (!string.IsNullOrEmpty(sedeId))
            query = query.Where(v => v.Bloques.Any(b => b.SedeId == sedeId));

        var turnos = await query
            .OrderBy(v => v.CodigoColaborador)
            .ThenBy(v => v.Fecha)
            .ToListAsync(ct);

        // Nunca 404: un rango sin turnos vigentes es 200 con Turnos: [], no un error.
        return new OkObjectResult(new ListaTurnosVigentes(
            desde, rangoAplicado.HastaAplicado, rangoAplicado.RangoRecortado, turnos));
    }
}
