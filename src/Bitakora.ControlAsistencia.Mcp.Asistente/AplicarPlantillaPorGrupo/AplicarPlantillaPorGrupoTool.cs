using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AplicarPlantillaPorGrupo;

public partial class AplicarPlantillaPorGrupoTool(
    ProgramacionApi programacion, SedesApi sedes,
    ResolutorPlantillaTurnosPorId resolutorPlantillas, ResolutorSedePorCodigo resolutorSedes, ResolutorCandidatosPorGrupo resolutorCandidatos,
    ILogger<AplicarPlantillaPorGrupoTool>? logger = null, TimeProvider? reloj = null)
{
    internal const string NombreTool = "aplicar_plantilla_por_grupo";

    [Function("AplicarPlantillaPorGrupo")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Aplica una plantilla de turnos a todos los colaboradores de un grupo, definido por sede y/o etiquetas, "
            + "durante una ventana de trabajo de maximo 35 dias; el ciclo empieza en fecha_inicio (dia 1). "
            + "Para personas concretas usa aplicar_plantilla. Ubica la plantilla con buscar_plantillas.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty("plantilla_id", "Id de la plantilla de turnos, tal como lo da buscar_plantillas.", isRequired: true)]
        string plantillaId,
        [McpToolProperty("fecha_inicio", "Fecha en que cae el dia 1 del ciclo, formato yyyy-MM-dd. Puede ser anterior a 'desde'.", isRequired: true)]
        string fechaInicio,
        [McpToolProperty("desde", "Primer dia de la ventana de trabajo, formato yyyy-MM-dd.", isRequired: true)]
        string desde,
        [McpToolProperty("hasta", "Ultimo dia de la ventana de trabajo, formato yyyy-MM-dd.", isRequired: true)]
        string hasta,
        [McpToolProperty("sede_de_programacion", "Opcional. Codigo de la sede donde se registra la programacion. Si prefiere la de cada colaborador, omitela.", isRequired: false)]
        string? sedeDeProgramacion,
        [McpToolProperty("sede", "Codigo de la sede de trabajo de los colaboradores del grupo.", isRequired: false)]
        string? sede,
        [McpToolProperty("etiquetas", "Pares categoria:valor separados por coma.", isRequired: false)]
        string? etiquetas,
        CancellationToken ct)
    {
        _ = (programacion, sedes, resolutorPlantillas, resolutorSedes, resolutorCandidatos, logger, reloj);
        throw new NotImplementedException();
    }
}
