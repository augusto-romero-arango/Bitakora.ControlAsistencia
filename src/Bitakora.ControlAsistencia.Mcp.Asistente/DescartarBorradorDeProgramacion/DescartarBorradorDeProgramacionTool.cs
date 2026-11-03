using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.DescartarBorradorDeProgramacion;

public partial class DescartarBorradorDeProgramacionTool(
    ProgramacionApi programacion, ColaboradoresApi colaboradores, TimeProvider? reloj = null)
{
    private readonly ProgramacionApi _programacion = programacion;
    private readonly ColaboradoresApi _colaboradores = colaboradores;
    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    internal const string NombreTool = "descartar_borrador_de_programacion";

    [Function("DescartarBorradorDeProgramacion")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Descarta el borrador de programacion de un colaborador: quita los turnos del borrador y "
            + "esos dias quedan sin programar. No toca lo publicado. Recibe la identificacion completa "
            + "tal como la devuelve buscar_colaboradores. Con desde y hasta (maximo 366 dias) descarta "
            + "los dias del borrador en ese periodo; sin periodo descarta todo el borrador pendiente del "
            + "colaborador tal como lo registra la consulta de pendientes. Descartar de nuevo lo ya "
            + "descartado no cambia nada.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": true, "idempotentHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "identificacion",
            "Identificacion completa del colaborador, tal como la devuelve buscar_colaboradores "
            + "(ej. 'CC-79879078').",
            isRequired: true)]
        string identificacion,
        [McpToolProperty("desde", "Primer dia del periodo, formato yyyy-MM-dd.", isRequired: false)]
        string? desde,
        [McpToolProperty("hasta", "Ultimo dia del periodo, formato yyyy-MM-dd.", isRequired: false)]
        string? hasta,
        CancellationToken ct)
        => throw new NotImplementedException();
}
