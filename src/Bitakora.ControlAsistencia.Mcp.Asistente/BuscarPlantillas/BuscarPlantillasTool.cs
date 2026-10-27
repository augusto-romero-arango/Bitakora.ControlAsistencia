using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.BuscarPlantillas;

public partial class BuscarPlantillasTool(ProgramacionApi api)
{
    internal const string NombreTool = "buscar_plantillas";
    internal const int MaximoPlantillas = 20;

    [Function("BuscarPlantillas")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Busca plantillas de turnos del catalogo para saber a cual se refiere el usuario cuando la "
            + "nombra de forma aproximada. Por nombre: palabras en cualquier orden, sin importar acentos "
            + "ni mayusculas, tambien incompletas o con una letra de diferencia. Por turno: plantillas "
            + "que usan en algun dia un turno con ese nombre aproximado. Por dias del ciclo: numero "
            + "exacto (1 a 42). Por estado: incompleta (algun dia sin turno) o con_advertencias (no "
            + "cumple su Jornada o no tiene Jornada). Combina los criterios. Devuelve hasta 20 "
            + "candidatas con el motivo de la coincidencia; si hay mas de una plausible, pregunta al "
            + "usuario antes de actuar. Para ver todo el catalogo usa listar_plantillas; para el "
            + "cuadro, obtener_plantilla.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("nombre", "Nombre o parte del nombre de la plantilla, en cualquier orden.")]
        string? nombre,
        [McpToolProperty("turno", "Nombre o parte del nombre de un turno que la plantilla usa en algun dia.")]
        string? turno,
        [McpToolProperty("dias_del_ciclo", "Numero de dias del ciclo de la plantilla, de 1 a 42.")]
        string? diasDelCiclo,
        [McpToolProperty("estado", "incompleta o con_advertencias.")]
        string? estado,
        CancellationToken ct) =>
        throw new NotImplementedException();
}

public sealed record CoincidenciasDePlantillas(
    int Mostrando,
    IReadOnlyList<PlantillaEncontrada> Plantillas,
    string? Nota);

public sealed record PlantillaEncontrada(
    string Id,
    string Nombre,
    int DiasDelCiclo,
    IReadOnlyList<string> CoincidePor,
    bool? Incompleta,
    int? Advertencias);
