using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.BuscarColaboradores;

public partial class BuscarColaboradoresTool(ColaboradoresApi api)
{
    internal const string NombreTool = "buscar_colaboradores";
    internal const int MaximoColaboradores = 20;
    internal const int TakeUpstream = 200;

    public Task<string> Run(
        ToolInvocationContext context, string? nombre, string? identificaciones, CancellationToken ct)
        => throw new NotImplementedException();
}
