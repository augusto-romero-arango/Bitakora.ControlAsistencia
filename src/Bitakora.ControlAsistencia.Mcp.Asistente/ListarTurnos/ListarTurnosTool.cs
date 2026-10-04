using System.Net.Http.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ListarTurnos;
public partial class ListarTurnosTool(ProgramacionApi api)
{
    internal const string NombreTool = "listar_turnos";
    internal const int MaximoTurnos = 50;

    public Task<string> Run(
        ToolInvocationContext context,
        string? filtroNombre,
        CancellationToken ct)
        => throw new NotImplementedException();
}
public sealed record CatalogoDeTurnos(
    int Total,
    int Mostrando,
    string? Nota,
    IReadOnlyList<TurnoResumido> Turnos);
public sealed record TurnoResumido(
    string Id,
    string Nombre,
    string Horario,
    bool? EsDescanso,
    bool? EnConstruccion);
