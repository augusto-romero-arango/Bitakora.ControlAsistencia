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
// Sin filtros ni paginacion en esta primera version (opcion B, decision de refinamiento): la UI trae
// el catalogo entero de un tiro y autocompleta en memoria -- el volumen es de decenas, no miles.
//
// La vista se serializa tal cual (sin DTO de respuesta): a diferencia de FichaColaborador (#356),
// CategoriaDeEtiquetas no tiene ningun campo interno de indexacion/filtrado que ocultar (ni
// centinela ni equivalente) -- MEF-ADR-0041 decision 4, "el DTO de respuesta es excepcion".
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ListarCategoriasDeEtiquetas")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "colaboradores/etiquetas/categorias")]
        HttpRequest req,
        CancellationToken ct)
    {
        // CA-6/MEF-ADR-0028: la QuerySession se abre SIEMPRE acotada al tenant que resuelve
        // ITenantResolver -- nunca a un tenant id que llegara por ruta o query string (mitigacion
        // estructural contra BOLA/IDOR, skills/projections/read-apis.md). Este GET no recibe ningun
        // segmento de ruta que pudiera confundirse con un tenant.
        await using var session = store.QuerySession(tenantResolver.TenantId);

        // Opcion B (decision de refinamiento): catalogo entero de un tiro, sin filtros ni
        // paginacion. CA-6: sin ninguna etiqueta asignada, coleccion vacia con 200 (nunca 404 --
        // una lista vacia es una respuesta valida, no un recurso ausente).
        //
        // OrderBy(Id) -- la categoria normalizada, que es la PK del documento: un SELECT sin ORDER
        // BY devuelve las filas en el orden fisico del heap de Postgres, que cambia cuando el
        // daemon reescribe una fila al aplicar un evento. Sin este orden dos consultas consecutivas
        // pueden devolver el mismo catalogo permutado, y el consumidor (autocompletado que cachea
        // el catalogo entre aperturas del control) veria un diff espurio. Mismo criterio que los
        // otros dos listados del BC (ListarFichasColaborador, ListarTurnosVigentes), aqui sin
        // cursor porque no hay paginacion.
        var categorias = await session.Query<CategoriaDeEtiquetas>()
            .OrderBy(categoria => categoria.Id)
            .ToListAsync(ct);

        return new OkObjectResult(categorias);
    }
}
