using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.MetadataRecursoProtegido;

// Protected Resource Metadata (RFC 9728), anonimo: el gate real es la politica de APIM, que reenvia
// aqui. Mcp:ResourceUri debe coincidir byte a byte con el <audiences> de esa politica y con el
// Resource Indicator (RFC 8707) que declara el cliente MCP. El host la sirve en
// /api/.well-known/oauth-protected-resource; la URL publica del PRM la fija APIM.
public class MetadataRecursoProtegidoFunction(IConfiguration configuration)
{
    [Function("MetadataRecursoProtegido")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ".well-known/oauth-protected-resource")]
        HttpRequest req)
    {
        var resource = configuration["Mcp:ResourceUri"];
        var authorizationServer = configuration["Mcp:AuthorizationServer"];

        // RFC 9728 exige URIs absolutas en ambos campos: el chequeo descarta a la vez el setting
        // ausente y el placeholder que el Terraform siembra hasta que existe el API de APIM.
        if (!Uri.TryCreate(resource, UriKind.Absolute, out _) ||
            !Uri.TryCreate(authorizationServer, UriKind.Absolute, out _))
            return new ObjectResult(
                "PRM sin configurar: Mcp__ResourceUri o Mcp__AuthorizationServer falta o sigue en placeholder.")
            { StatusCode = StatusCodes.Status503ServiceUnavailable };

        return new OkObjectResult(new
        {
            resource,
            authorization_servers = new[] { authorizationServer }
        });
    }
}
