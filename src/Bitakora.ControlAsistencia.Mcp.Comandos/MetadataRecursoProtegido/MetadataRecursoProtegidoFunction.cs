using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.MetadataRecursoProtegido;

// Protected Resource Metadata (RFC 9728): descubrimiento anonimo que un cliente OAuth (flujo
// MCP/Connect) usa para arrancar la autorizacion (MEF-ADR-0032 seccion 9). Defensa en profundidad
// -- el gate real vive en la politica dedicada de APIM, que reenvia a este backend anonimo
// (MEF-ADR-0047 decision 7). Mcp:ResourceUri debe coincidir byte a byte con el <audiences> de esa
// politica y con el Resource Indicator (RFC 8707) que declara el cliente MCP.
//
// Ruta efectiva: el host sirve esta Function bajo el routePrefix por defecto ("api"), o sea en
// /api/.well-known/oauth-protected-resource. La ruta raiz que exige RFC 9728 la publica el borde
// de APIM, mapeando /.well-known/oauth-protected-resource a esta.
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
