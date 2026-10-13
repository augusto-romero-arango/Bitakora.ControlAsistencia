using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AsignarJornadaPredeterminada;

public partial class AsignarJornadaPredeterminadaTool(ProgramacionApi api)
{
    internal const string NombreTool = "asignar_jornada_predeterminada";

    public Task<string> Run(ToolInvocationContext context, string jornadaId, CancellationToken ct) =>
        throw new NotImplementedException();
}
