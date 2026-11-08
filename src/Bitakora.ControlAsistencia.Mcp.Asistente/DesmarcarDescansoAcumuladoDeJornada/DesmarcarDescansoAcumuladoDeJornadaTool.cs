using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.DesmarcarDescansoAcumuladoDeJornada;

public partial class DesmarcarDescansoAcumuladoDeJornadaTool(ProgramacionApi api)
{
    internal const string NombreTool = "desmarcar_descanso_acumulado_de_jornada";

    [Function("DesmarcarDescansoAcumuladoDeJornada")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Quita la marca de descanso acumulado: la semana sin dia de descanso vuelve a medirse semana a semana.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false, "idempotentHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("jornada_id", "Identificador de la Jornada, tal como lo da listar_jornadas.", isRequired: true)]
        string jornadaId,
        CancellationToken ct)
        => throw new NotImplementedException();
}
