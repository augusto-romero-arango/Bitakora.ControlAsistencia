using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Mcp.Comandos;

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

        // SourceRevisionId se hornea en InformationalVersion como "{Version}+{SourceRevisionId}"
        // (SDK de .NET desde la 8.0, Source Link -- MEF-ADR-0031). Sin el separador '+' (build
        // local sin SourceRevisionId) no hay SHA que extraer.
        var indiceSeparador = informationalVersion?.IndexOf('+') ?? -1;
        var sha = indiceSeparador >= 0 ? informationalVersion![(indiceSeparador + 1)..] : null;

        return new OkObjectResult(new { sha });
    }
}
