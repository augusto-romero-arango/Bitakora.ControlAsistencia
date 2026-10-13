using Bitakora.ControlAsistencia.ReadModels.Colaboradores;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.ListarCategoriasDeEtiquetas;

// Issue #357: Function GET del catalogo CategoriaDeEtiquetas (via (a) proyeccion materializada,
// skills/projections/naming.md, MEF-ADR-0006 enmienda #363). Feature folder sin sufijo Function, un
// namespace por query (skills/projections/read-apis.md): esta clase FunctionEndpoint no colisiona
// con ninguna otra del mismo ensamblado porque cada query vive en su propio namespace.
//
// Sin filtros. Keyset por Id con take opcional (max 200) y sobre { elementos, siguienteCursor } con
// cursor opaco, sin total (CA-ADR-0039); sin take devuelve el catalogo entero (CA-ADR-0038).
//
// La vista se serializa tal cual (sin DTO de respuesta): a diferencia de FichaColaborador (#356),
// CategoriaDeEtiquetas no tiene ningun campo interno de indexacion/filtrado que ocultar (ni
// centinela ni equivalente) -- MEF-ADR-0041 decision 4, "el DTO de respuesta es excepcion".
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
        // CA-6/MEF-ADR-0028: la QuerySession se abre SIEMPRE acotada al tenant que resuelve
        // ITenantContext -- nunca a un tenant id que llegara por ruta o query string (mitigacion
        // estructural contra BOLA/IDOR, skills/projections/read-apis.md). Este GET no recibe ningun
        // segmento de ruta que pudiera confundirse con un tenant.
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

        await using var session = store.QuerySession(tenantContext.TenantId);

        // Sin categorias: 200 con elementos vacios, nunca 404. OrderBy(Id) -- la categoria
        // normalizada, PK del documento -- es la clave keyset y ademas evita el orden fisico del heap
        // de Postgres, que cambia cuando el daemon reescribe una fila.
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
