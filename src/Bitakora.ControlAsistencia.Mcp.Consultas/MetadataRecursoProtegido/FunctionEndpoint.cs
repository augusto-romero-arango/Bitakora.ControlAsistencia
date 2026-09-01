using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.MetadataRecursoProtegido;

// Anonimo (CA-3): un cliente MCP sin token todavia debe poder leer este documento para saber
// contra que authorization server autenticarse (spec de autorizacion MCP). Queda bajo el
// routePrefix "api" por defecto en vez del root que RFC 8615 sugeriria: vaciarlo en host.json
// moveria tambien /api/ready y /api/version, ya desplegadas. El cliente no deriva esta URL por
// convencion -- la lee del parametro resource_metadata del WWW-Authenticate.
public class FunctionEndpoint(ConstructorMetadataRecursoProtegido constructor)
{
    internal const string NombreFuncion = "MetadataRecursoProtegido";
    internal const string Ruta = ".well-known/oauth-protected-resource";

    [Function(NombreFuncion)]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = Ruta)]
        HttpRequest req) => new OkObjectResult(constructor.Construir());
}
