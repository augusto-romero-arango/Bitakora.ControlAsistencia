using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarDiasSinProgramar;

public partial class ConsultarDiasSinProgramarTool(
    ControlHorasApi controlHoras, ResolutorCandidatosPorGrupo resolutor, TimeProvider reloj)
{
    internal const string NombreTool = "consultar_dias_sin_programar";

    [Function("ConsultarDiasSinProgramar")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Consulta a quien le faltan dias por programar: que colaboradores tienen, dentro de su "
            + "vinculacion, dias sin turno, sin descanso y sin ausencia en una o varias semanas (lunes a "
            + "domingo). Sin fecha revisa la PROXIMA semana; semanas (1 a 5) amplia la ventana. Filtra por "
            + "sede, etiquetas (pares categoria:valor) o codigos de colaborador; sin filtros revisa a todos. "
            + "Solo aparecen quienes tienen dias sin programar. Para ver los turnos de alguien, usa "
            + "consultar_programacion.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("fecha", "Cualquier dia de la primera semana a revisar, formato yyyy-MM-dd. Si se omite, la proxima semana.")]
        string? fecha,
        [McpToolProperty("semanas", "Cantidad de semanas a revisar, de 1 a 5 (por defecto 1).")]
        int? semanas,
        [McpToolProperty("sede", "Codigo de la sede para revisar solo a sus colaboradores.")]
        string? sede,
        [McpToolProperty("etiquetas", "Pares categoria:valor separados por coma; combina todos en AND.")]
        string? etiquetas,
        [McpToolProperty("codigos_colaborador", "Codigos de colaborador separados por coma para casos puntuales.")]
        string? codigosColaborador,
        [McpToolProperty("cursor", "El siguienteCursor de la respuesta anterior, tal cual, para ver la pagina siguiente.")]
        string? cursor,
        CancellationToken ct)
    {
        _ = (controlHoras, resolutor, reloj);
        await Task.CompletedTask;
        throw new NotImplementedException();
    }
}
