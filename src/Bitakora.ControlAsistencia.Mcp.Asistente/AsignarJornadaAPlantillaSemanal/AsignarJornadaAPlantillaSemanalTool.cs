using System.Net;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AsignarJornadaAPlantillaSemanal;

public partial class AsignarJornadaAPlantillaSemanalTool(
    ProgramacionApi programacion, ResolutorPlantillaPorId resolutorPlantilla)
{
    internal const string NombreTool = "asignar_jornada_a_plantilla_semanal";

    [Function("AsignarJornadaAPlantillaSemanal")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Asocia una Jornada a una plantilla semanal (por su id) o la reemplaza si ya tenia otra. "
            + "La plantilla se audita contra los limites de esa Jornada; las advertencias se ven con "
            + "obtener_plantilla_semanal en unos segundos. El jornada_id sale de listar_jornadas.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false, "idempotentHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "plantilla_id", "Id de la plantilla semanal, tal como lo da buscar_plantillas_semanales.", isRequired: true)]
        string plantillaId,
        [McpToolProperty("jornada_id", "Identificador de la Jornada, tal como lo da listar_jornadas.", isRequired: true)]
        string jornadaId,
        CancellationToken ct)
    {
        _ = (programacion, resolutorPlantilla);
        throw new NotImplementedException();
    }
}

public sealed record JornadaAsignadaResumen(
    string Resultado, string Plantilla, JornadaDescritaResumen Jornada, string Nota);
