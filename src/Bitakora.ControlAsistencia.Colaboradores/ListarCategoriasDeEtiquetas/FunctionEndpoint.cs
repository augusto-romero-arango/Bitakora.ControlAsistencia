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
// Stub de fase roja (projection-test-writer, MEF-ADR-0033): el constructor fija la forma que el
// test de composicion (Colaboradores.Tests/Infraestructura/ComposicionServiciosTests.cs,
// AgregarServiciosColaboradores_ResuelveElEndpointDeListarCategoriasDeEtiquetas_...) resuelve del
// contenedor DI real -- IDocumentStore y ITenantResolver, ya registrados por
// ComposicionServicios.AgregarServiciosColaboradores. La apertura de la QuerySession acotada al
// tenant del resolver, session.Query&lt;CategoriaDeEtiquetas&gt;() y el 200 con coleccion vacia
// cuando no hay etiquetas asignadas (CA-6) son responsabilidad de projection-implementer -- Run solo
// lanza NotImplementedException.
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ListarCategoriasDeEtiquetas")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "colaboradores/etiquetas/categorias")]
        HttpRequest req,
        CancellationToken ct) =>
        throw new NotImplementedException();
}
