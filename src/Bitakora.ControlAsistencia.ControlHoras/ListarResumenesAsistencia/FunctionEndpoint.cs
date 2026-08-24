using System.Text.Json;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarResumenesAsistencia;

// Function QUERY (RFC 10008, MEF-ADR-0042) sobre la vista materializada AsistenciaDiaria, via (a')
// de MEF-ADR-0035, con agregacion en query-time.
//
// La paginacion keyset va en DOS PASOS y no en un GroupBy: Marten no lo traduce a SQL, asi que la
// pagina se resuelve primero sobre los CODIGOS -- de la lista pedida, o descubiertos con un distinct
// sobre el rango -- y solo despues se traen los documentos de esos codigos para agregarlos en
// memoria. Verificado contra Marten 9.12.0 + Postgres 16 (revision de este PR): el paso de
// descubrimiento -- Select + Distinct + OrderBy + Take, con el filtro de cursor CompareTo(...) > 0
// aplicado antes del Select -- traduce y pagina sobre codigos distintos, no sobre filas.
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ListarResumenesAsistencia")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "query", Route = "control-horas/resumenes-asistencia")]
        HttpRequest req,
        CancellationToken ct)
    {
        // El 415 va ANTES de leer el body: ante un Content-Type no-JSON, ReadFromJsonAsync lanza
        // una excepcion que NO es JsonException y escaparia como 500 pese al catch de abajo.
        if (!req.HasJsonContentType())
            return new ObjectResult("La query exige Content-Type: application/json")
            { StatusCode = StatusCodes.Status415UnsupportedMediaType };

        FiltroListarResumenesAsistencia? filtro;
        try
        {
            filtro = await req.ReadFromJsonAsync<FiltroListarResumenesAsistencia>(ct);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult("El body de la query no es un JSON valido");
        }

        if (filtro is null)
            return new BadRequestObjectResult("El body de la query es obligatorio");

        if (filtro.DesdeFecha is null || filtro.HastaFecha is null)
            return NoProcesable("DesdeFecha y HastaFecha son obligatorios");

        if (filtro.DesdeFecha > filtro.HastaFecha)
            return NoProcesable("DesdeFecha no puede ser posterior a HastaFecha");

        var desde = filtro.DesdeFecha.Value;
        var rangoAplicado = RangoConsulta.Recortar(desde, filtro.HastaFecha.Value);
        var hastaAplicado = rangoAplicado.HastaAplicado;
        var take = PaginaDeCodigos.AcotarTake(filtro.Take);

        // Sesion acotada al tenant que resuelve ITenantResolver, nunca a un dato de la request
        // (mitigacion estructural contra BOLA/IDOR, MEF-ADR-0028).
        await using var session = store.QuerySession(tenantResolver.TenantId);

        var codigosPagina = await DeterminarCodigosPaginaAsync(
            session, filtro.CodigosColaborador, filtro.Cursor, desde, hastaAplicado, take, ct);

        IReadOnlyList<AsistenciaDiaria> documentos = codigosPagina.Count == 0
            ? []
            : await session.Query<AsistenciaDiaria>()
                .Where(a => a.Fecha >= desde
                            && a.Fecha <= hastaAplicado
                            && codigosPagina.Contains(a.CodigoColaborador))
                .ToListAsync(ct);

        var filas = AgregadorResumenAsistencia.Agregar(desde, hastaAplicado, codigosPagina, documentos);

        // Nunca 404: un rango sin datos son filas SinDatos (o una lista vacia cuando no hay
        // CodigosColaborador pedido), no un error.
        return new OkObjectResult(new ListaResumenesAsistencia(
            desde, hastaAplicado, rangoAplicado.RangoRecortado, filas));
    }

    // Con CodigosColaborador explicito la pagina se recorta sobre la lista pedida, sin consultar
    // Marten: el cliente ya trae el universo. Sin ella, se descubre con un distinct sobre el rango.
    //
    // Las dos ramas ordenan ascendente y acotan por cursor (">") y Take, pero NO con el mismo
    // comparador, y no pueden: en memoria es ordinal y en Marten es la collation de Postgres.
    // Medido en la revision de este PR sobre un mismo conjunto: Postgres devuelve
    // emp-1, EMP_1, EMP-10, EMP-2, EMP-9, EMPA y el ordinal EMP-10, EMP-2, EMP-9, EMPA, EMP_1,
    // emp-1 -- coinciden solo mientras los codigos sean homogeneos en case y separadores. Cada rama
    // si es coherente consigo misma (su filtro de cursor usa el mismo comparador que su orden), que
    // es lo que sostiene la paginacion: alinear una sola de las dos la romperia.
    private static async Task<IReadOnlyList<string>> DeterminarCodigosPaginaAsync(
        IQuerySession session,
        IReadOnlyList<string>? codigosSolicitados,
        string? cursor,
        DateOnly desde,
        DateOnly hastaAplicado,
        int take,
        CancellationToken ct)
    {
        if (codigosSolicitados is not null)
            return PaginaDeCodigos.Recortar(codigosSolicitados, cursor, take);

        IQueryable<AsistenciaDiaria> query = session.Query<AsistenciaDiaria>()
            .Where(a => a.Fecha >= desde && a.Fecha <= hastaAplicado);

        if (cursor is not null)
            query = query.Where(a => a.CodigoColaborador.CompareTo(cursor) > 0);

        return await query
            .Select(a => a.CodigoColaborador)
            .Distinct()
            .OrderBy(codigo => codigo)
            .Take(take)
            .ToListAsync(ct);
    }

    // RFC 10008 seccion 2.1 / MEF-ADR-0042 seccion 3: el 422 se emite como ObjectResult con mensaje,
    // nunca como codigo pelado.
    private static ObjectResult NoProcesable(string mensaje) =>
        new(mensaje) { StatusCode = StatusCodes.Status422UnprocessableEntity };
}
