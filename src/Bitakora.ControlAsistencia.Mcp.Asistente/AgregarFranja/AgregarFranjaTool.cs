using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AgregarFranja;
public partial class AgregarFranjaTool(ProgramacionApi programacion, SedesApi sedes)
{
    internal const string NombreTool = "agregar_franja";

    public Task<string> Run(
        ToolInvocationContext context,
        string turno,
        string inicio,
        string fin,
        string? codigoSede,
        CancellationToken ct)
        => throw new NotImplementedException();
}
public sealed record FranjaAgregadaResumen(string Resultado, string Turno, string Franja, string Nota);
