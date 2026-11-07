using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AsignarModalidadAJornada;

public partial class AsignarModalidadAJornadaTool(ProgramacionApi api)
{
    internal const string NombreTool = "asignar_modalidad_a_jornada";

    [Function("AsignarModalidadAJornada")]
    public Task<string> Run(
        [McpToolTrigger(NombreTool, "Declara la modalidad de una Jornada.")]
        ToolInvocationContext context,
        string jornadaId,
        string modalidad,
        CancellationToken ct)
        => throw new NotImplementedException();
}
