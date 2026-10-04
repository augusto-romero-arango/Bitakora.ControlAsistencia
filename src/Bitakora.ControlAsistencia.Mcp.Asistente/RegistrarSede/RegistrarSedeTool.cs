using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.RegistrarSede;

public partial class RegistrarSedeTool(SedesApi api)
{
    internal const string NombreTool = "registrar_sede";

    public Task<string> Run(
        ToolInvocationContext context,
        string codigo,
        string nombre,
        string? ciudad,
        string? direccion,
        CancellationToken ct)
        => throw new NotImplementedException();
}
