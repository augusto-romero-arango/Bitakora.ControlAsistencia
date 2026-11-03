using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.PublicarProgramacion;

public partial class PublicarProgramacionTool(ProgramacionApi programacion, ColaboradoresApi colaboradores)
{
    internal const string NombreTool = "publicar_programacion";

    [Function("PublicarProgramacion")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Publica el borrador de programacion de un colaborador en un periodo (desde, hasta; "
            + "maximo 366 dias): los dias de ese periodo que estaban en el borrador quedan en firme "
            + "y rigen para el calculo. Los dias con ausencia siguen en el borrador. Recibe la "
            + "identificacion completa tal como la devuelve buscar_colaboradores. Publicar de nuevo "
            + "lo ya publicado no cambia nada.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false, "idempotentHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "identificacion",
            "Identificacion completa del colaborador, tal como la devuelve buscar_colaboradores "
            + "(ej. 'CC-79879078').",
            isRequired: true)]
        string identificacion,
        [McpToolProperty("desde", "Primer dia del periodo, formato yyyy-MM-dd.", isRequired: true)]
        string desde,
        [McpToolProperty("hasta", "Ultimo dia del periodo, formato yyyy-MM-dd.", isRequired: true)]
        string hasta,
        CancellationToken ct)
    {
        _ = (programacion, colaboradores);
        throw new NotImplementedException();
    }
}
