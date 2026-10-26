using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.QuitarJornadaDePlantillaSemanal;

public partial class QuitarJornadaDePlantillaSemanalTool(
    ProgramacionApi programacion, ResolutorPlantillaPorId resolutorPlantilla)
{
    internal const string NombreTool = "quitar_jornada_de_plantilla_semanal";

    [Function("QuitarJornadaDePlantillaSemanal")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Quita la Jornada de una plantilla semanal (por su id): la plantilla deja de auditarse "
            + "contra limites. Si no tenia Jornada, no cambia nada.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": true, "idempotentHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "plantilla_id", "Id de la plantilla semanal, tal como lo da buscar_plantillas_semanales.", isRequired: true)]
        string plantillaId,
        CancellationToken ct)
    {
        _ = (programacion, resolutorPlantilla);
        throw new NotImplementedException();
    }
}

public sealed record JornadaQuitadaResumen(string Resultado, string Plantilla, string Nota);
