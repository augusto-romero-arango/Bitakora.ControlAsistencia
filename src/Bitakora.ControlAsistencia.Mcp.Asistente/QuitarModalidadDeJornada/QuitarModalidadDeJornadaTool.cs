using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.QuitarModalidadDeJornada;

public partial class QuitarModalidadDeJornadaTool(ProgramacionApi api)
{
    internal const string NombreTool = "quitar_modalidad_de_jornada";

    [Function("QuitarModalidadDeJornada")]
    public Task<string> Run(
        [McpToolTrigger(NombreTool, "Quita la modalidad de una Jornada.")]
        ToolInvocationContext context,
        string jornadaId,
        CancellationToken ct)
        => throw new NotImplementedException();
}
