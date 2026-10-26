using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.RegistrarColaboradores;

public partial class RegistrarColaboradoresTool(ColaboradoresApi api)
{
    internal const string NombreTool = "registrar_colaboradores";

    private readonly ColaboradoresApi _api = api;

    [Function("RegistrarColaboradores")]
    public Task<string> Run(
        [McpToolTrigger(NombreTool, "stub")]
        ToolInvocationContext context,
        string colaboradores,
        CancellationToken ct)
        => throw new NotImplementedException();
}
