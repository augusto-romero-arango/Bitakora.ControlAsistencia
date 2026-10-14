using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.QuitarJornadaDePlantillaSemanal;

public partial class QuitarJornadaDePlantillaSemanalTool(
    ProgramacionApi programacion, ResolutorPlantillaPorNombre resolutorPlantilla)
{
    internal const string NombreTool = "quitar_jornada_de_plantilla_semanal";

    [Function("QuitarJornadaDePlantillaSemanal")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Quita la Jornada de una plantilla semanal (nombre exacto): la plantilla deja de auditarse "
            + "contra limites. Si no tenia Jornada, no cambia nada.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": true, "idempotentHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "plantilla",
            "Nombre exacto de la plantilla semanal del catalogo (mirala con listar_plantillas_semanales).",
            isRequired: true)]
        string plantilla,
        CancellationToken ct) =>
        throw new NotImplementedException();
}

public sealed record JornadaQuitadaResumen(string Resultado, string Plantilla, string Nota);
