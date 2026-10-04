using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.CrearTurno;
public partial class CrearTurnoTool(ProgramacionApi programacion)
{
    internal const string NombreTool = "crear_turno";

    public Task<string> Run(
        ToolInvocationContext context,
        string nombre,
        bool? esDescanso,
        CancellationToken ct)
        => throw new NotImplementedException();
}
public sealed record TurnoCreadoResumen(string Resultado, TurnoCreadoEco Turno, string Nota);

public sealed record TurnoCreadoEco(string Id, string Nombre, bool EsDescanso, bool Completo);
