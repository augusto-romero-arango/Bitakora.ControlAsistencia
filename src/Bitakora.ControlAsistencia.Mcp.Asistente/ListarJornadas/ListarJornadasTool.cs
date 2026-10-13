using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ListarJornadas;

public partial class ListarJornadasTool(ProgramacionApi api)
{
    internal const string NombreTool = "listar_jornadas";
    internal const int MaximoJornadas = 50;

    public Task<string> Run(ToolInvocationContext context, string? cursor, CancellationToken ct) =>
        throw new NotImplementedException();
}
