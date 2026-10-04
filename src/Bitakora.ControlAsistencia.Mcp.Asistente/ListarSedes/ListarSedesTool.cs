using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ListarSedes;

public class ListarSedesTool(SedesApi api)
{
    internal const string NombreTool = "listar_sedes";

    public Task<string> Run(ToolInvocationContext context, string? filtroNombre, CancellationToken ct)
        => throw new NotImplementedException();
}
