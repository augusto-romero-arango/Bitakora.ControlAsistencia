using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.MetadataRecursoProtegido;

// Anonimo (CA-3): un cliente MCP sin token todavia debe poder leer este documento para saber
// contra que authorization server autenticarse (spec de autorizacion MCP). La ruta
// ".well-known/..." puede exigir routePrefix vacio o un proxy en host.json -- decision del
// implementer.
public class FunctionEndpoint(ConstructorMetadataRecursoProtegido constructor)
{
    [Function("MetadataRecursoProtegido")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ".well-known/oauth-protected-resource")]
        HttpRequest req) => throw new NotImplementedException();
}
