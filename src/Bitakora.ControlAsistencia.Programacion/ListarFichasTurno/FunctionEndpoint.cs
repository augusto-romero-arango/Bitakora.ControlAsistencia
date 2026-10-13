using Bitakora.ControlAsistencia.ReadModels.Programacion;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.ListarFichasTurno;

// Catalogo paginado: keyset por (Nombre, Id), take opcional (max 200), sobre { elementos, siguienteCursor }
// con cursor opaco, sin total (CA-ADR-0039). Comparte la ruta con el POST de creacion; cada uno declara su verbo.
public class FunctionEndpoint(IDocumentStore store, ITenantContext tenantContext)
{
    private const int TakeMaximo = 200;

    [Function("ListarFichasTurno")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "programacion/turnos")]
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

        CursorFichaTurno? cursor = null;
        if (req.Query.TryGetValue("cursor", out var cursorCrudo) && !string.IsNullOrEmpty(cursorCrudo[0]))
        {
            cursor = CursorFichaTurno.Decodificar(cursorCrudo[0]!);
            if (cursor is null)
                return new BadRequestObjectResult("El cursor no es valido");
        }

        // MEF-ADR-0028: la QuerySession se abre SIEMPRE acotada al tenant que resuelve
        // ITenantContext -- nunca a un tenant id que llegara por query string.
        await using var session = store.QuerySession(tenantContext.TenantId);

        // El orden (Nombre, Id) es ordinal y se aplica en memoria: el catalogo es acotado y asi el
        // corte keyset no depende de la colacion de la base.
        var todas = await session.Query<FichaTurno>().ToListAsync(ct);
        var posteriores = todas
            .OrderBy(x => x.Nombre, StringComparer.Ordinal).ThenBy(x => x.Id, StringComparer.Ordinal)
            .Where(x => cursor is not { } c
                || string.CompareOrdinal(x.Nombre, c.Nombre) > 0
                || (x.Nombre == c.Nombre && string.CompareOrdinal(x.Id, c.Id) > 0));
        var pedidas = (take is { } t ? posteriores.Take(t + 1) : posteriores).ToList();

        return new OkObjectResult(PaginaFichasTurno.Cortar(pedidas, take));
    }
}
