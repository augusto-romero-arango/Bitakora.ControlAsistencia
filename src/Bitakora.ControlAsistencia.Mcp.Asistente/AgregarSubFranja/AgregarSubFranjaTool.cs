using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AgregarSubFranja;
public partial class AgregarSubFranjaTool(ProgramacionApi programacion)
{
    internal const string NombreTool = "agregar_subfranja";

    public Task<string> Run(
        ToolInvocationContext context,
        string turno,
        string franja,
        string tipo,
        string inicio,
        string fin,
        CancellationToken ct)
        => throw new NotImplementedException();
}
public sealed record SubFranjaAgregadaResumen(
    string Resultado, string Turno, string Franja, string SubFranja, string Nota);
