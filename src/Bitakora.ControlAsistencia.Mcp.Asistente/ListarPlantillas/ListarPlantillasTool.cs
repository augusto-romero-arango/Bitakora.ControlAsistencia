using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ListarPlantillas;

public partial class ListarPlantillasTool(ProgramacionApi api)
{
    internal const string NombreTool = "listar_plantillas";
    internal const int MaximoPlantillas = 50;

    [Function("ListarPlantillas")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Lista las plantillas de turnos del catalogo: nombre, dias del ciclo y si esta lista para "
            + "usarse. Las incompletas y las que no cumplen su Jornada aparecen marcadas. Muestra 50 "
            + "por pagina; si trae siguienteCursor, pasalo como cursor. Para ubicar una plantilla que "
            + "el usuario nombra de forma aproximada usa buscar_plantillas; para su cuadro, "
            + "obtener_plantilla.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("cursor", "Cursor opaco de la pagina siguiente (el siguienteCursor de la respuesta anterior); reenvialo tal cual.")]
        string? cursor,
        CancellationToken ct) =>
        throw new NotImplementedException();
}

public sealed record CatalogoDePlantillas(
    int Mostrando,
    IReadOnlyList<PlantillaResumida> Plantillas,
    string? SiguienteCursor,
    string? Nota);

public sealed record PlantillaResumida(
    string Id,
    string Nombre,
    int DiasDelCiclo,
    bool? Incompleta,
    bool? ConAdvertencias);
