using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ObtenerPlantilla;

public partial class ObtenerPlantillaTool(ProgramacionApi api)
{
    internal const string NombreTool = "obtener_plantilla";

    [Function("ObtenerPlantilla")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Devuelve el cuadro de una plantilla de turnos por su id (ubicala con buscar_plantillas): "
            + "por cada dia del ciclo, del 1 al N, el turno con su nombre y horario, los dias sin turno "
            + "y los turnos retirados o incompletos; la Jornada asociada y las advertencias de la "
            + "plantilla contra sus limites. Para las franjas de un turno usa obtener_turno.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "plantilla_id", "Id de la plantilla de turnos, tal como lo da buscar_plantillas.", isRequired: true)]
        string plantillaId,
        CancellationToken ct) =>
        throw new NotImplementedException();
}

public sealed record PlantillaDetallada(
    string Id,
    string Nombre,
    int DiasDelCiclo,
    bool Completa,
    IReadOnlyList<DiaDeLaPlantilla> Dias,
    JornadaDescritaResumen? Jornada,
    IReadOnlyList<string> Advertencias);

public sealed record DiaDeLaPlantilla(int Dia, string Turno);
