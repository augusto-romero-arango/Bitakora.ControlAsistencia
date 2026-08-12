using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.ObtenerFichaColaborador;

// Issue #356: Function GET del read model FichaColaborador (via (a) proyeccion materializada,
// skills/projections/naming.md, MEF-ADR-0006 enmienda #363). Feature folder sin sufijo Function, un
// namespace por query (skills/projections/read-apis.md): esta clase FunctionEndpoint no colisiona
// con ninguna otra del mismo ensamblado porque cada query vive en su propio namespace.
//
// Stub de fase roja (projection-test-writer, MEF-ADR-0033): el constructor fija la forma que el
// test de composicion (Colaboradores.Tests/Infraestructura/ComposicionServiciosTests.cs,
// AgregarServiciosColaboradores_ResuelveElEndpointDeObtenerFichaColaborador_...) resuelve del
// contenedor DI real -- IDocumentStore y ITenantResolver, ya registrados por
// ComposicionServicios.AgregarServiciosColaboradores. El parseo tipado de tipoIdentificacion/numero
// (MEF-ADR-0037: ComputarStreamId, nunca una concatenacion propia del endpoint), la apertura de la
// QuerySession, el 400/404/200 y la traduccion centinela -> vacio (CA-6) son responsabilidad de
// projection-implementer -- Run solo lanza NotImplementedException.
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ObtenerFichaColaborador")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "colaboradores/fichas/{tipoIdentificacion}/{numero}")]
        HttpRequest req,
        string tipoIdentificacion,
        string numero,
        CancellationToken ct) =>
        throw new NotImplementedException();
}
