using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AsignarTurnoADiaDePlantilla;

public partial class AsignarTurnoADiaDePlantillaTool(
    ProgramacionApi programacion, ResolutorPlantillaTurnosPorId resolutorPlantilla, ResolutorTurnoPorId resolutorTurno)
{
    internal const string NombreTool = "asignar_turno_a_dia_de_plantilla";

    [Function("AsignarTurnoADiaDePlantilla")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Pone o reemplaza el turno de un dia de una plantilla de turnos: id de la plantilla "
            + "(ubicala con buscar_plantillas), dia del ciclo (1..N) e id del turno (ubicalo con "
            + "buscar_turnos). Solo admite turnos completos; el descanso es un turno mas. Si el dia "
            + "ya tenia ese mismo turno, no cambia nada.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "plantilla_id", "Id de la plantilla de turnos, tal como lo da buscar_plantillas.", isRequired: true)]
        string plantillaId,
        [McpToolProperty("dia", "Dia del ciclo de la plantilla (1..N).", isRequired: true)]
        int? dia,
        [McpToolProperty("turno_id", "Id del turno, tal como lo da buscar_turnos.", isRequired: true)]
        string turnoId,
        CancellationToken ct)
        => throw new NotImplementedException();
}
