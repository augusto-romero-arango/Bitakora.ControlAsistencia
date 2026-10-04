using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AplicarPlantillaSemanalPorGrupo;

public partial class AplicarPlantillaSemanalPorGrupoTool(
    ProgramacionApi programacion, SedesApi sedes, ColaboradoresApi colaboradores,
    ILogger<AplicarPlantillaSemanalPorGrupoTool>? logger = null, TimeProvider? reloj = null)
{
    internal const string NombreTool = "aplicar_plantilla_semanal_por_grupo";

    [Function("AplicarPlantillaSemanalPorGrupo")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Aplica una plantilla semanal de turnos a todos los colaboradores de un grupo, definido por sede "
            + "y/o etiquetas (ambos criterios se combinan), durante una ventana de trabajo de maximo 35 dias. "
            + "La sede de programacion es opcional: sugierela al usuario y, si prefiere la sede de cada "
            + "colaborador, omite el parametro; no es la sede de trabajo ni el selector sede del grupo. Para "
            + "personas concretas usa aplicar_plantilla_semanal.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty("desde", "Primer dia de la ventana de trabajo, formato yyyy-MM-dd.", isRequired: true)]
        string desde,
        [McpToolProperty("hasta", "Ultimo dia de la ventana de trabajo, formato yyyy-MM-dd.", isRequired: true)]
        string hasta,
        [McpToolProperty("plantilla", "Nombre de la plantilla semanal; miralo con listar_plantillas_semanales.", isRequired: true)]
        string plantilla,
        [McpToolProperty(
            "sede_de_programacion",
            "Opcional. Codigo de la sede donde se registra la programacion; sugierela al usuario. "
            + "Si prefiere la de cada colaborador, omitela. No es la sede de trabajo del grupo.",
            isRequired: false)]
        string? sedeDeProgramacion,
        [McpToolProperty("sede", "Codigo de la sede de trabajo de los colaboradores del grupo.", isRequired: false)]
        string? sede,
        [McpToolProperty("etiquetas", "Pares categoria:valor separados por coma.", isRequired: false)]
        string? etiquetas,
        CancellationToken ct)
        => throw new NotImplementedException();
}
