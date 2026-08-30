using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Mcp.Consultas;

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

        var separatorIndex = informationalVersion?.IndexOf('+') ?? -1;
        var sha = separatorIndex >= 0 ? informationalVersion![(separatorIndex + 1)..] : string.Empty;

        return new OkObjectResult(sha);
    }
}
