using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.RetirarTurno;
public partial class RetirarTurnoTool(ProgramacionApi programacion)
{
    internal const string NombreTool = "retirar_turno";

    public Task<string> Run(
        ToolInvocationContext context,
        string turno,
        CancellationToken ct)
        => throw new NotImplementedException();
}
public sealed record TurnoRetiradoResumen(string Resultado, TurnoRetiradoEco Turno, string Nota);

public sealed record TurnoRetiradoEco(string Id, string Nombre);
