using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AsignarJornadaAPlantillaSemanal;

public partial class AsignarJornadaAPlantillaSemanalTool(
    ProgramacionApi programacion, ResolutorPlantillaPorNombre resolutorPlantilla)
{
    internal const string NombreTool = "asignar_jornada_a_plantilla_semanal";

    [Function("AsignarJornadaAPlantillaSemanal")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Asocia una Jornada a una plantilla semanal (nombre exacto) o la reemplaza si ya tenia otra. "
            + "La plantilla se audita contra los limites de esa Jornada; las advertencias se ven con "
            + "obtener_plantilla_semanal en unos segundos. El jornada_id sale de listar_jornadas.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false, "idempotentHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "plantilla",
            "Nombre exacto de la plantilla semanal del catalogo (mirala con listar_plantillas_semanales).",
            isRequired: true)]
        string plantilla,
        [McpToolProperty("jornada_id", "Identificador de la Jornada, tal como lo da listar_jornadas.", isRequired: true)]
        string jornadaId,
        CancellationToken ct) =>
        throw new NotImplementedException();
}

public sealed record JornadaAsignadaResumen(
    string Resultado, string Plantilla, JornadaDescritaResumen Jornada, string Nota);

public sealed record JornadaDescritaResumen(string JornadaId, string Descripcion);
