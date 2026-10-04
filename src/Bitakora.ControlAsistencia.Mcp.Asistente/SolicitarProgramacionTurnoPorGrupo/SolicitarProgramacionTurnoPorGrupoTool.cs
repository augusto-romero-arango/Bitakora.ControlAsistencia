using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurnoPorGrupo;

public partial class SolicitarProgramacionTurnoPorGrupoTool(
    ProgramacionApi programacion, SedesApi sedes, ColaboradoresApi colaboradores)
{
    internal const string NombreTool = "solicitar_programacion_turno_por_grupo";

    [Function("SolicitarProgramacionTurnoPorGrupo")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Programa un turno a todos los colaboradores de un grupo, definido por sede y/o etiquetas "
            + "(ambos criterios se combinan), para los dias de una ventana de maximo 35 dias, en una "
            + "sede de programacion.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty("desde", "Primer dia de la ventana de trabajo, formato yyyy-MM-dd.", isRequired: true)]
        string desde,
        [McpToolProperty("hasta", "Ultimo dia de la ventana de trabajo, formato yyyy-MM-dd.", isRequired: true)]
        string hasta,
        [McpToolProperty("turno", "Nombre exacto del turno del catalogo.", isRequired: true)]
        string turno,
        [McpToolProperty("sede_de_programacion", "Codigo de la sede donde se registra la programacion.", isRequired: true)]
        string sedeDeProgramacion,
        [McpToolProperty("sede", "Codigo de la sede de trabajo de los colaboradores del grupo.", isRequired: false)]
        string? sede,
        [McpToolProperty("etiquetas", "Pares categoria:valor separados por coma.", isRequired: false)]
        string? etiquetas,
        CancellationToken ct) => throw new NotImplementedException();
}
