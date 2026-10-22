using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ModificarVentanaDeEntrada;

public partial class ModificarVentanaDeEntradaTool(ProgramacionApi programacion, ResolutorTurnoPorNombre resolutor)
{
    private readonly ProgramacionApi _programacion = programacion;
    private readonly ResolutorTurnoPorNombre _resolutor = resolutor;

    internal const string NombreTool = "modificar_ventana_de_entrada";

    [Function("ModificarVentanaDeEntrada")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Modifica la ventana de entrada de un turno del catalogo, indicado por su nombre exacto: el rango, "
            + "desde el inicio de su primera franja, dentro del cual la entrada del colaborador corre todo el turno. "
            + "amplitud en formato H:mm; 0:00 = turno fijo. Una ventana mayor que cero exige un turno con franjas, "
            + "que la primera franja empiece desde las 04:00 y que la ultima franja mas la amplitud termine antes "
            + "de las 24:00. No cambia lo ya programado.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false, "idempotentHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "turno", "Nombre exacto del turno del catalogo (miralo con buscar_turnos).", isRequired: true)]
        string turno,
        [McpToolProperty("amplitud", "Amplitud de la ventana en formato H:mm, por ejemplo 3:00.", isRequired: true)]
        string amplitud,
        CancellationToken ct) =>
        throw new NotImplementedException();
}
