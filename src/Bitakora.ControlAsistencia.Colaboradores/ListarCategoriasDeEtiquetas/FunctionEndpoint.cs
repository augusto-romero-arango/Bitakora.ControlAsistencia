using Bitakora.ControlAsistencia.ReadModels.Colaboradores;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.ListarCategoriasDeEtiquetas;

// Keyset por Id, sin total (CA-ADR-0039); sin take devuelve el catalogo entero (CA-ADR-0038).
// Sin DTO de respuesta: la vista no tiene campos internos que ocultar (MEF-ADR-0041 decision 4).
public class FunctionEndpoint(IDocumentStore store, ITenantContext tenantContext)
{
    private const int TakeMaximo = 200;

    internal static ListaCategoriasDeEtiquetas Componer(IReadOnlyList<CategoriaDeEtiquetas> filas, int? take)
    {
        if (take is not { } t || filas.Count <= t)
            return new ListaCategoriasDeEtiquetas(filas, null);

        var pagina = filas.Take(t).ToList();
        return new ListaCategoriasDeEtiquetas(pagina, CursorCategoria.Codificar(pagina[^1].Id));
    }

    [Function("ListarCategoriasDeEtiquetas")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "colaboradores/etiquetas/categorias")]
        HttpRequest req,
        CancellationToken ct)
    {
        int? take = null;
        if (req.Query.TryGetValue("take", out var takeCrudo) && takeCrudo.Count > 0)
        {
            if (!int.TryParse(takeCrudo[0], out var valor) || valor < 1 || valor > TakeMaximo)
                return new BadRequestObjectResult($"take debe ser un entero entre 1 y {TakeMaximo}");
            take = valor;
        }

        string? cursorId = null;
        if (req.Query.TryGetValue("cursor", out var cursorCrudo) && !string.IsNullOrEmpty(cursorCrudo[0]))
        {
            if (!CursorCategoria.TryDecodificar(cursorCrudo[0]!, out var decodificado))
                return new BadRequestObjectResult("El cursor no es valido");
            cursorId = decodificado;
        }

        // Siempre el tenant de ITenantContext, nunca uno que llegue por ruta o query (BOLA/IDOR).
        await using var session = store.QuerySession(tenantContext.TenantId);

        // Sin categorias: 200 con elementos vacios, nunca 404. OrderBy(Id) ademas evita el orden
        // fisico del heap de Postgres, que cambia cuando el daemon reescribe una fila.
        IQueryable<CategoriaDeEtiquetas> query = session.Query<CategoriaDeEtiquetas>();
        if (cursorId is not null)
            query = query.Where(categoria => categoria.Id.CompareTo(cursorId) > 0);

        var ordenada = query.OrderBy(categoria => categoria.Id);
        var filas = take is { } t
            ? await ordenada.Take(t + 1).ToListAsync(ct)
            : await ordenada.ToListAsync(ct);

        return new OkObjectResult(Componer(filas, take));
    }
}
