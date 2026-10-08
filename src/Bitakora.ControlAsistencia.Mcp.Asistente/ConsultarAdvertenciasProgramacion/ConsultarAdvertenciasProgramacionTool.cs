using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarAdvertenciasProgramacion;

public partial class ConsultarAdvertenciasProgramacionTool(
    ControlHorasApi controlHoras, ResolutorCandidatosPorGrupo resolutor, TimeProvider reloj)
{
    internal const string NombreTool = "consultar_advertencias_programacion";
    internal const int MaximoColaboradores = 50;

    [Function("ConsultarAdvertenciasProgramacion")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Consulta las advertencias de la programacion semanal: que colaboradores tienen la "
            + "programacion de una semana (lunes a domingo) fuera de su Jornada y por cuanto -- superan "
            + "el tope diario o las horas semanales, quedan por debajo del minimo diario o de las horas "
            + "semanales, o tienen dias de descanso de mas o de menos. Sin fecha revisa la PROXIMA "
            + "semana. Filtra por sede, etiquetas (pares categoria:valor) o codigos de colaborador. "
            + "Solo aparecen quienes tienen advertencias. Para ver los turnos de alguien, usa "
            + "consultar_programacion.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("fecha", "Cualquier dia de la semana a revisar, formato yyyy-MM-dd. Si se omite, la proxima semana.")]
        string? fecha,
        [McpToolProperty("sede", "Codigo de la sede para revisar solo a sus colaboradores.")]
        string? sede,
        [McpToolProperty("etiquetas", "Pares categoria:valor separados por coma; combina todos en AND.")]
        string? etiquetas,
        [McpToolProperty("codigos_colaborador", "Codigos de colaborador separados por coma para casos puntuales.")]
        string? codigosColaborador,
        [McpToolProperty("cursor", "El siguienteCursor de la respuesta anterior, tal cual, para ver la pagina siguiente.")]
        string? cursor,
        CancellationToken ct) => throw new NotImplementedException();
}

public sealed record AdvertenciasDeLaSemana(
    string Desde,
    string Hasta,
    int Mostrando,
    IReadOnlyList<ColaboradorConAdvertencias> Colaboradores,
    string? SiguienteCursor,
    string? Nota);

public sealed record ColaboradorConAdvertencias(string Codigo, string Nombre, IReadOnlyList<string> Advertencias);
