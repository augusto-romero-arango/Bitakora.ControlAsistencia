using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.ProgramarAusencia;

public partial class ProgramarAusenciaTool(ProgramacionApi programacion, ColaboradoresApi colaboradores)
{
    internal const string NombreTool = "programar_ausencia";

    public Task<string> Run(
        ToolInvocationContext context,
        string identificacion,
        string desde,
        string hasta,
        string motivo,
        CancellationToken ct)
        => throw new NotImplementedException();
}
