using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AsignarJornadaAPlantilla;

public partial class AsignarJornadaAPlantillaTool(
    ProgramacionApi programacion, ResolutorPlantillaTurnosPorId resolutorPlantilla)
{
    internal const string NombreTool = "asignar_jornada_a_plantilla";

    [Function("AsignarJornadaAPlantilla")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Asocia una Jornada a una plantilla de turnos (por su id) o la reemplaza si ya tenia otra. "
            + "La plantilla se audita contra los limites de esa Jornada; las advertencias se ven con "
            + "obtener_plantilla en unos segundos. El jornada_id sale de listar_jornadas.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false, "idempotentHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "plantilla_id", "Id de la plantilla de turnos, tal como lo da buscar_plantillas.", isRequired: true)]
        string plantillaId,
        [McpToolProperty("jornada_id", "Identificador de la Jornada, tal como lo da listar_jornadas.", isRequired: true)]
        string jornadaId,
        CancellationToken ct)
        => throw new NotImplementedException();
}
