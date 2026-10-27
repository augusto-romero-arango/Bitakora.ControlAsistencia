using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.RetirarPlantilla;

public partial class RetirarPlantillaTool(ProgramacionApi programacion, ResolutorPlantillaTurnosPorId resolutor)
{
    internal const string NombreTool = "retirar_plantilla";

    [Function("RetirarPlantilla")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Retira una plantilla de turnos del catalogo por su id (ubicala con buscar_plantillas): "
            + "deja de poder usarse y su nombre queda libre. Los turnos del catalogo y lo ya "
            + "programado con ella no cambian.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "plantilla_id", "Id de la plantilla de turnos, tal como lo da buscar_plantillas.", isRequired: true)]
        string plantillaId,
        CancellationToken ct)
        => throw new NotImplementedException();
}
