using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AsignarJornadaPorGrupo;

public partial class AsignarJornadaPorGrupoTool(
    ProgramacionApi programacion, ColaboradoresApi colaboradores, ResolutorSedePorCodigo resolutorSedes,
    ResolutorCandidatosPorGrupo resolutorCandidatos, TimeProvider? reloj = null)
{
    internal const string NombreTool = "asignar_jornada_por_grupo";

    [Function("AsignarJornadaPorGrupo")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Asigna una Jornada a todos los colaboradores vigentes de un grupo, definido por sede y/o "
            + "etiquetas (los criterios se combinan). Sirve para reasignar en bloque antes de desactivar "
            + "una Jornada. Los fallidos se reintentan con asignar_jornada.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false, "idempotentHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("jornada_id", "Identificador de la Jornada, tal como lo da listar_jornadas.", isRequired: true)]
        string jornadaId,
        [McpToolProperty("sede", "Codigo de la sede de trabajo de los colaboradores del grupo.", isRequired: false)]
        string? sede,
        [McpToolProperty("etiquetas", "Pares categoria:valor separados por coma.", isRequired: false)]
        string? etiquetas,
        CancellationToken ct) =>
        throw new NotImplementedException();
}
