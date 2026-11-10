using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Mcp.Asistente;

public class VersionCheck
{
    [Function("version")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "version")]
        HttpRequest req)
    {
        var informationalVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        // El SDK de .NET (8.0+) hornea InformationalVersion como "{Version}+{SourceRevisionId}"; sin el
        // '+' (build local sin SourceRevisionId) no hay SHA que extraer.
        var indiceSeparador = informationalVersion?.IndexOf('+') ?? -1;
        var sha = indiceSeparador >= 0 ? informationalVersion![(indiceSeparador + 1)..] : null;

        return new OkObjectResult(new { sha });
    }
}
