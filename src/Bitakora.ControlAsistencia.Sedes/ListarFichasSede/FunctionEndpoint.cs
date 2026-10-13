using Bitakora.ControlAsistencia.ReadModels.Sedes;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Sedes.ListarFichasSede;

// Issue #461 (creacion): Function GET de listado sobre FichaSede -- mismo segmento de recurso que
// ObtenerFichaSede ("sedes/fichas"), sin QUERY: el filtro Activa es un unico par campo=valor en
// igualdad (MEF-ADR-0042 seccion 1). Keyset por (Codigo, Id), take opcional (max 200), sobre
// { elementos, siguienteCursor } sin total (CA-ADR-0039).
//
// CA-6: sin filtro devuelve todas las fichas; "?activa=true"/"?activa=false" filtra por la bandera
// de asignabilidad.
public class FunctionEndpoint(IDocumentStore store, ITenantContext tenantContext)
{
    private const int TakeMaximo = 200;

    [Function("ListarFichasSede")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sedes/fichas")]
        HttpRequest req,
        CancellationToken ct)
    {
        string? filtroActivaCrudo = req.Query["activa"];
        bool? filtroActiva = null;
        if (!string.IsNullOrEmpty(filtroActivaCrudo))
        {
            if (!bool.TryParse(filtroActivaCrudo, out var filtroActivaTipado))
                return new BadRequestObjectResult("El filtro 'activa' debe ser true o false");

            filtroActiva = filtroActivaTipado;
        }

        int? take = null;
        if (req.Query.TryGetValue("take", out var takeCrudo) && takeCrudo.Count > 0)
        {
            if (!int.TryParse(takeCrudo[0], out var valor) || valor < 1 || valor > TakeMaximo)
                return new BadRequestObjectResult($"take debe ser un entero entre 1 y {TakeMaximo}");
            take = valor;
        }

        CursorFichaSede? cursor = null;
        if (req.Query.TryGetValue("cursor", out var cursorCrudo) && !string.IsNullOrEmpty(cursorCrudo[0]))
        {
            cursor = CursorFichaSede.Decodificar(cursorCrudo[0]!);
            if (cursor is null)
                return new BadRequestObjectResult("El cursor no es valido");
        }

        // CA-6/MEF-ADR-0028: la QuerySession se abre SIEMPRE acotada al tenant que resuelve
        // ITenantContext -- nunca a un tenant id que llegara por query string.
        await using var session = store.QuerySession(tenantContext.TenantId);

        IQueryable<FichaSede> query = session.Query<FichaSede>();

        // El predicado captura un bool NO nullable: comparar contra el bool? cerrado
        // (f.Activa == filtroActiva) arma una igualdad lifted -- Convert(f.Activa, bool?) -- que
        // el proveedor LINQ de Marten no declara traducible, y su modo de falla seria un 500 en
        // runtime que ningun test unitario ve (mismo tipo de trampa que MEF-ADR-0042 seccion 6
        // registra para CompareTo sobre string).
        if (filtroActiva is { } activa)
            query = query.Where(f => f.Activa == activa);

        if (cursor is { } c)
            query = query.Where(f => f.Codigo.CompareTo(c.Codigo) > 0
                || (f.Codigo == c.Codigo && f.Id.CompareTo(c.Id) > 0));

        var ordenada = query.OrderBy(f => f.Codigo).ThenBy(f => f.Id);

        var pedidas = take is { } t
            ? await ordenada.Take(t + 1).ToListAsync(ct)
            : await ordenada.ToListAsync(ct);

        return new OkObjectResult(PaginaFichasSede.Cortar(pedidas, take));
    }
}
