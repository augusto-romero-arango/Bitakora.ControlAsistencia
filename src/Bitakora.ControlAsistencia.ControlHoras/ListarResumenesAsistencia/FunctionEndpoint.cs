using System.Text.Json;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarResumenesAsistencia;

// Issue #428: Function QUERY (RFC 10008, MEF-ADR-0042) sobre la vista materializada AsistenciaDiaria
// (#426), via (a') de MEF-ADR-0035, con agregacion en query-time (AgregadorResumenAsistencia) --
// este issue NO crea proyeccion, read model ni lifecycle nuevos.
//
// Paginacion keyset en DOS PASOS (riesgo tecnico anotado por el planner, "Investigacion del
// planner"): (1) pagina de codigos de colaborador -- de la lista pedida si CodigosColaborador viene
// explicito (sin consultar Marten: el cliente ya trae el universo), o descubierta con un distinct
// sobre AsistenciaDiaria en el rango si no viene -- ordenada ascendente y acotada por cursor/Take;
// (2) documentos de esos codigos en el rango, agregados en memoria (AgregadorResumenAsistencia). La
// traducibilidad exacta del Select().Distinct() del paso de descubrimiento a SQL queda NO VERIFICADA
// (sin spike propio, a diferencia del CompareTo de string que #373 si verifico) -- el smoke test
// contra dev es quien la confirma end-to-end (ver resumen de este stage, "Desviaciones").
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    // MEF-ADR-0042 seccion 2 / patron heredado de #373: el Take del cliente jamas llega crudo a
    // Marten.
    private const int TakeMaximo = 200;

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
        var take = Math.Clamp(filtro.Take, 1, TakeMaximo);

        // Sesion acotada al tenant que resuelve ITenantResolver, nunca a un dato de la request
        // (mitigacion estructural contra BOLA/IDOR, MEF-ADR-0028).
        await using var session = store.QuerySession(tenantResolver.TenantId);

        var codigosPagina = await DeterminarCodigosPaginaAsync(
            session, filtro.CodigosColaborador, filtro.Cursor, desde, hastaAplicado, take, ct);

        var documentos = await session.Query<AsistenciaDiaria>()
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

    // CA-3/CA-4: con CodigosColaborador explicito, la pagina keyset se recorta sobre la lista
    // pedida (el cliente ya trae el universo, sin consultar Marten para descubrirlo); sin el, se
    // descubre con un distinct sobre los documentos del rango. Ambas ramas ordenan ascendente por
    // CodigoColaborador y acotan por cursor (">") y Take -- mismo contrato keyset que #373.
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
        {
            IEnumerable<string> ordenados = codigosSolicitados
                .Distinct(StringComparer.Ordinal)
                .OrderBy(codigo => codigo, StringComparer.Ordinal);

            if (cursor is not null)
                ordenados = ordenados.Where(codigo => string.CompareOrdinal(codigo, cursor) > 0);

            return ordenados.Take(take).ToList();
        }

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
