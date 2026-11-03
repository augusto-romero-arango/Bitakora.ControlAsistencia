using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.PublicarProgramacionPorGrupo;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.DescartarBorradorDeProgramacionPorGrupo;

public partial class DescartarBorradorDeProgramacionPorGrupoTool(
    ProgramacionApi programacion, ResolutorSedePorCodigo resolutorSedes,
    ResolutorCandidatosPorGrupo resolutorCandidatos, TimeProvider? reloj = null)
{
    internal const string NombreTool = "descartar_borrador_de_programacion_por_grupo";

    [Function("DescartarBorradorDeProgramacionPorGrupo")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Descarta el borrador de programacion de un grupo de colaboradores, definido por sede y/o etiquetas (los dos criterios se combinan), o de todos los que tienen borrador pendiente (todo_lo_pendiente): quita esos turnos del borrador y esos dias quedan sin programar. No toca lo publicado. Con desde y hasta descarta los dias del borrador en ese rango (maximo 366 dias); sin rango descarta todos los dias pendientes de cada colaborador. Para un colaborador usa descartar_borrador_de_programacion; los fallidos se reintentan con esa tool.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("sede", "Codigo de la sede de trabajo de los colaboradores del grupo.", isRequired: false)]
        string? sede,
        [McpToolProperty("etiquetas", "Pares categoria:valor separados por coma.", isRequired: false)]
        string? etiquetas,
        [McpToolProperty("todo_lo_pendiente", "true = todos los colaboradores con borrador pendiente.", isRequired: false)]
        bool? todoLoPendiente,
        [McpToolProperty("desde", "Primer dia del rango, formato yyyy-MM-dd.", isRequired: false)]
        string? desde,
        [McpToolProperty("hasta", "Ultimo dia del rango, formato yyyy-MM-dd.", isRequired: false)]
        string? hasta,
        CancellationToken ct) => throw new NotImplementedException();
}

public sealed record DescartarBorradorDeProgramacionPorGrupoResumen(
    string Resultado, string Alcance, string Rango, int Colaboradores,
    IReadOnlyList<ColaboradorFallidoDeGrupo>? Fallidos, string Nota);
