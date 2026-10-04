using System.Net;
using System.Net.Http.Json;
using System.Text;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ObtenerTurno;
public partial class ObtenerTurnoTool(ProgramacionApi api)
{
    internal const string NombreTool = "obtener_turno";

    public Task<string> Run(
        ToolInvocationContext context,
        string id,
        CancellationToken ct)
        => throw new NotImplementedException();
}
public sealed record TurnoDetallado(
    string Id,
    string Nombre,
    bool EsDescanso,
    bool Completo,
    string Horario,
    IReadOnlyList<string> Franjas);
