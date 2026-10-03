using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.CancelarAusencia;

public partial class CancelarAusenciaTool(ProgramacionApi programacion, ColaboradoresApi colaboradores)
{
    internal const string NombreTool = "cancelar_ausencia";

    public Task<string> Run(
        ToolInvocationContext context,
        string identificacion,
        string desde,
        string hasta,
        bool? completa,
        CancellationToken ct)
        => throw new NotImplementedException();
}
