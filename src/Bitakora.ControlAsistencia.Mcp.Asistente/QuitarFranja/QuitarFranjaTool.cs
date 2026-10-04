using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.QuitarFranja;
public partial class QuitarFranjaTool(ProgramacionApi programacion)
{
    internal const string NombreTool = "quitar_franja";

    public Task<string> Run(
        ToolInvocationContext context,
        string turno,
        string franja,
        CancellationToken ct)
        => throw new NotImplementedException();
}
public sealed record FranjaQuitadaResumen(string Resultado, string Turno, string FranjaQuitada, string Nota);
