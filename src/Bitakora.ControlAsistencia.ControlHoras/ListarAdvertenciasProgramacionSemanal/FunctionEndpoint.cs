using System.Text.Json;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarAdvertenciasProgramacionSemanal;

// Function QUERY (RFC 10008, MEF-ADR-0042) sobre la vista materializada AdvertenciasProgramacionSemanal,
// via (a') de MEF-ADR-0035. Una semana ISO a la vez; keyset por CodigoColaborador con cursor opaco
// y Take opcional (CA-ADR-0039).
public class FunctionEndpoint(IDocumentStore store, ITenantContext tenantContext)
{
    [Function("ListarAdvertenciasProgramacionSemanal")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "query", Route = "control-horas/advertencias-programacion-semanal")]
        HttpRequest req,
        CancellationToken ct)
    {
        // El 415 va antes de leer el body: ReadFromJsonAsync lanza una excepcion que no es JsonException.
        if (!req.HasJsonContentType())
            return new ObjectResult("La query exige Content-Type: application/json")
            { StatusCode = StatusCodes.Status415UnsupportedMediaType };

        FiltroListarAdvertenciasProgramacionSemanal? filtro;
        try
        {
            filtro = await req.ReadFromJsonAsync<FiltroListarAdvertenciasProgramacionSemanal>(ct);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult("El body de la query no es un JSON valido");
        }

        if (filtro is null)
            return new BadRequestObjectResult("El body de la query es obligatorio");

        if (filtro.Fecha is null)
            return NoProcesable(Mensajes.Obtener("FechaObligatoria"));

        string? cursorCodigo = null;
        if (filtro.Cursor is not null)
        {
            if (!CursorOpaco.TryDecodificar(filtro.Cursor, out var decodificado))
                return NoProcesable(Mensajes.Obtener("CursorInvalido"));
            cursorCodigo = decodificado;
        }

        var semana = SemanaIso.De(filtro.Fecha.Value);
        var anio = semana.Anio;
        var numero = semana.Numero;
        var take = PaginaDeAdvertencias.AcotarTake(filtro.Take);
        var soloConAdvertencias = filtro.SoloConAdvertencias;
        var codigos = filtro.CodigosColaborador is { Count: > 0 } c ? c.ToList() : null;

        // Sesion acotada al tenant que resuelve ITenantContext, nunca a un dato de la request (MEF-ADR-0028).
        await using var session = store.QuerySession(tenantContext.TenantId);

        IQueryable<AdvertenciasProgramacionSemanal> query = session.Query<AdvertenciasProgramacionSemanal>()
            .Where(a => a.AnioIso == anio && a.NumeroSemana == numero);

        if (codigos is not null)
            query = query.Where(a => codigos.Contains(a.CodigoColaborador));
        if (soloConAdvertencias)
            query = query.Where(a => a.TieneAdvertencias);
        if (cursorCodigo is not null)
            query = query.Where(a => a.CodigoColaborador.CompareTo(cursorCodigo) > 0);

        var ordenada = query.OrderBy(a => a.CodigoColaborador);
        var filas = take is null
            ? await ordenada.ToListAsync(ct)
            : await ordenada.Take(take.Value + 1).ToListAsync(ct);

        var (pagina, siguienteCursor) = PaginaDeAdvertencias.Cortar(filas, take, f => f.CodigoColaborador);

        // Nunca 404: una semana sin documentos es una lista vacia.
        return new OkObjectResult(new ListaAdvertenciasProgramacionSemanal(
            semana.Lunes, semana.Domingo, semana.Anio, semana.Numero,
            pagina.Select(PresentadorAdvertencias.Presentar).ToList(), siguienteCursor));
    }

    private static ObjectResult NoProcesable(string mensaje) =>
        new(mensaje) { StatusCode = StatusCodes.Status422UnprocessableEntity };
}
