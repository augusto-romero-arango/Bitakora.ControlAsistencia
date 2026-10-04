using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.QuitarSubFranja;
public partial class QuitarSubFranjaTool(ProgramacionApi programacion)
{
    internal const string NombreTool = "quitar_subfranja";

    public Task<string> Run(
        ToolInvocationContext context,
        string turno,
        string franja,
        string tipo,
        string inicio,
        CancellationToken ct)
        => throw new NotImplementedException();
}
public sealed record SubFranjaQuitadaResumen(
    string Resultado, string Turno, string Franja, string SubFranjaQuitada, string Nota);
