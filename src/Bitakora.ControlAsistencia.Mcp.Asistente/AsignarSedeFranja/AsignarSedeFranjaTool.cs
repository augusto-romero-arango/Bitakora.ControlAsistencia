using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AsignarSedeFranja;
public partial class AsignarSedeFranjaTool(ProgramacionApi programacion, SedesApi sedes)
{
    internal const string NombreTool = "asignar_sede_franja";

    public Task<string> Run(
        ToolInvocationContext context,
        string turno,
        string franja,
        string? codigoSede,
        CancellationToken ct)
        => throw new NotImplementedException();
}
public sealed record SedeDeFranjaResumen(string Resultado, string Turno, string Franja, string? Sede, string Nota);
