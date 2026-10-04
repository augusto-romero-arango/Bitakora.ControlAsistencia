using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ListarColaboradores;

public partial class ListarColaboradoresTool(ColaboradoresApi api, TimeProvider reloj)
{
    internal const string NombreTool = "listar_colaboradores";
    internal const int MaximoColaboradores = 20;
    internal const int TakeUpstream = 200;

    public Task<string> Run(
        ToolInvocationContext context,
        string? identificacion,
        string? sede,
        string? etiquetas,
        string? fechaReferencia,
        CancellationToken ct)
        => throw new NotImplementedException();
}
