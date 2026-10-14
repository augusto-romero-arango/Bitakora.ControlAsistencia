using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.BuscarTurnos;

public partial class BuscarTurnosTool(ProgramacionApi api)
{
    internal const string NombreTool = "buscar_turnos";
    internal const int MaximoTurnos = 20;

    [Function("BuscarTurnos")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Busca turnos del catalogo para saber a cual se refiere el usuario cuando lo nombra de "
            + "forma aproximada.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("nombre", "Nombre o parte del nombre del turno.")]
        string? nombre,
        [McpToolProperty("horario", "Hora (HH:mm) o rango (HH:mm-HH:mm).")]
        string? horario,
        [McpToolProperty("sede", "Codigo o nombre de la sede de alguna franja.")]
        string? sede,
        CancellationToken ct)
        => throw new NotImplementedException();
}

public sealed record CoincidenciasDeTurnos(
    int Mostrando,
    IReadOnlyList<TurnoEncontrado> Turnos,
    string? Nota);

public sealed record TurnoEncontrado(
    string Id,
    string Nombre,
    string Horario,
    IReadOnlyList<string> CoincidePor,
    bool? EsDescanso,
    bool? EnConstruccion);
