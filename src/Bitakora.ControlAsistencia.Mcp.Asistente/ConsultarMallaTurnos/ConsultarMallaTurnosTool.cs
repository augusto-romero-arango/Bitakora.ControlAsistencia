using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarMallaTurnos;

public partial class ConsultarMallaTurnosTool
{
    private readonly ControlHorasApi _controlHoras;
    private readonly ResolutorCandidatosPorGrupo _resolutor;
    private readonly TimeProvider _reloj;

    public ConsultarMallaTurnosTool(
        ControlHorasApi controlHoras, ResolutorCandidatosPorGrupo resolutor, TimeProvider reloj)
    {
        _controlHoras = controlHoras;
        _resolutor = resolutor;
        _reloj = reloj;
    }

    internal const string NombreTool = "consultar_malla_turnos";
    internal const int MaximoColaboradores = 50;

    [Function("ConsultarMallaTurnos")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Consulta la malla semanal de turnos.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("fecha", "Cualquier dia de la semana a revisar, formato yyyy-MM-dd.")]
        string? fecha,
        [McpToolProperty("sede", "Codigo de la sede.")]
        string? sede,
        [McpToolProperty("etiquetas", "Pares categoria:valor separados por coma.")]
        string? etiquetas,
        [McpToolProperty("codigos_colaborador", "Codigos de colaborador separados por coma.")]
        string? codigosColaborador,
        [McpToolProperty("cursor", "El siguienteCursor de la respuesta anterior.")]
        string? cursor,
        CancellationToken ct) => throw new NotImplementedException();
}
