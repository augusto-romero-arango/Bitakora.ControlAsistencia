using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AplicarPlantillaSemanal;

public partial class AplicarPlantillaSemanalTool(
    ProgramacionApi programacion, SedesApi sedes, ColaboradoresApi colaboradores,
    ILogger<AplicarPlantillaSemanalTool>? logger = null, TimeProvider? reloj = null)
{
    internal const string NombreTool = "aplicar_plantilla_semanal";

    [Function("AplicarPlantillaSemanal")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Aplica una plantilla semanal de turnos a una lista de colaboradores durante una ventana de "
            + "trabajo de maximo 35 dias. A cada dia le programa el turno que el molde asigna a esa semana "
            + "y dia; el molde se alinea a la semana (lunes a domingo) que contiene 'desde'. Recibe el "
            + "nombre de la plantilla (miralo con listar_plantillas_semanales), opcionalmente el codigo de "
            + "la sede de programacion -- sugierela al usuario y, si prefiere la sede de cada colaborador, "
            + "omite el parametro -- y las identificaciones completas separadas por coma.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty("desde", "Primer dia de la ventana de trabajo, formato yyyy-MM-dd.", isRequired: true)]
        string desde,
        [McpToolProperty("hasta", "Ultimo dia de la ventana de trabajo, formato yyyy-MM-dd.", isRequired: true)]
        string hasta,
        [McpToolProperty("plantilla", "Nombre de la plantilla semanal; miralo con listar_plantillas_semanales.", isRequired: true)]
        string plantilla,
        [McpToolProperty("sede_de_programacion", "Opcional. Codigo de la sede donde se registra la programacion.", isRequired: false)]
        string? sedeDeProgramacion,
        [McpToolProperty("identificaciones", "Identificaciones completas separadas por coma ('CC-79879078, CE-887766').", isRequired: true)]
        string identificaciones,
        CancellationToken ct) => throw new NotImplementedException();
}
