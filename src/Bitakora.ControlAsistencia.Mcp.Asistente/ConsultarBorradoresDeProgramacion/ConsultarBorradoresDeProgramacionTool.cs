using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarBorradoresDeProgramacion;

public partial class ConsultarBorradoresDeProgramacionTool(
    ProgramacionApi api, ResolutorCandidatosPorGrupo resolutor, TimeProvider reloj)
{
    internal const string NombreTool = "consultar_borradores_de_programacion";
    internal const int MaximoColaboradores = 50;

    [Function("ConsultarBorradoresDeProgramacion")]
    public Task<string> Run(
        [McpToolTrigger(NombreTool, "Pendiente de implementar.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("desde", "Fecha inicial del periodo, formato yyyy-MM-dd.")]
        string? desde,
        [McpToolProperty("hasta", "Fecha final del periodo (inclusive), formato yyyy-MM-dd.")]
        string? hasta,
        [McpToolProperty("sede", "Codigo de la sede para ver solo a sus colaboradores.")]
        string? sede,
        [McpToolProperty("etiquetas", "Pares categoria:valor separados por coma; combina todos en AND.")]
        string? etiquetas,
        [McpToolProperty("codigos_colaborador", "Codigos de colaborador separados por coma.")]
        string? codigosColaborador,
        [McpToolProperty("cursor", "El siguienteCursor de la respuesta anterior, tal cual.")]
        string? cursor,
        CancellationToken ct) => throw new NotImplementedException();
}
