using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AplicarPlantilla;

public partial class AplicarPlantillaTool(
    ProgramacionApi programacion, SedesApi sedes,
    ResolutorPlantillaTurnosPorId resolutorPlantillas, ResolutorSedePorCodigo resolutorSedes, ResolutorCandidatosPorLista resolutorCandidatos,
    ILogger<AplicarPlantillaTool>? logger = null, TimeProvider? reloj = null)
{
    internal const string NombreTool = "aplicar_plantilla";

    [Function("AplicarPlantilla")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Aplica una plantilla de turnos a una lista de colaboradores durante una ventana de trabajo de maximo 35 dias. "
            + "El ciclo empieza en fecha_inicio (dia 1) y se repite cada N dias: a cada fecha le programa el turno de su dia del ciclo. "
            + "fecha_inicio puede ser anterior a la ventana; para continuar una rotacion ya aplicada, usa la misma fecha_inicio de la vez anterior (preguntala al usuario). "
            + "Los dias de la ventana anteriores a fecha_inicio no se programan. Recibe el id de la plantilla (ubicala con buscar_plantillas), "
            + "la sede de programacion opcional y las identificaciones completas separadas por coma. "
            + "A cada colaborador le programa solo los dias que su vinculacion cubre y respeta sus ausencias.")]
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
        [McpToolProperty("sede_de_programacion", "Opcional. Codigo de la sede donde se registra la programacion; sugierela al usuario. Si prefiere la de cada colaborador, omitela.", isRequired: false)]
        string? sedeDeProgramacion,
        [McpToolProperty("identificaciones", "Identificaciones completas separadas por coma ('CC-79879078, CE-887766').", isRequired: true)]
        string identificaciones,
        CancellationToken ct)
    {
        _ = (programacion, sedes, resolutorPlantillas, resolutorSedes, resolutorCandidatos, logger, reloj);
        throw new NotImplementedException();
    }
}
