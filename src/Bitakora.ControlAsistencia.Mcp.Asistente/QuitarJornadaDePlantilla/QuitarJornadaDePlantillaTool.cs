using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.QuitarJornadaDePlantilla;

public partial class QuitarJornadaDePlantillaTool(
    ProgramacionApi programacion, ResolutorPlantillaTurnosPorId resolutorPlantilla)
{
    internal const string NombreTool = "quitar_jornada_de_plantilla";

    [Function("QuitarJornadaDePlantilla")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Quita la Jornada de una plantilla de turnos (por su id): la plantilla deja de auditarse "
            + "contra limites. Si no tenia Jornada, no cambia nada.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false, "idempotentHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "plantilla_id", "Id de la plantilla de turnos, tal como lo da buscar_plantillas.", isRequired: true)]
        string plantillaId,
        CancellationToken ct)
        => throw new NotImplementedException();
}
