using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.ListarPlantillasSemanales;

// Tool de solo lectura sobre GET programacion/plantillas-semanales (issue #629). La respuesta de
// #625 ya viene compuesta (cuadro + fichas de turno); esta tool solo remodela para
// token-eficiencia -- no junta nada (CA-ADR-0034 decision 5 enmendada). El detalle con franjas de
// cada turno sigue siendo obtener_turno; el cuadro dia por dia es obtener_plantilla_semanal.
public partial class ListarPlantillasSemanalesTool(ProgramacionApi api)
{
    internal const string NombreTool = "listar_plantillas_semanales";
    internal const int MaximoPlantillas = 50;

    [Function("ListarPlantillasSemanales")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Lista las plantillas semanales de turnos del catalogo: nombre, numero de semanas y si "
            + "esta lista para usarse. Las incompletas aparecen marcadas. La lista se trunca cuando "
            + "es larga; usa filtro_nombre para encontrar una especifica. Para ver el cuadro de una "
            + "plantilla usa obtener_plantilla_semanal.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "filtro_nombre",
            "Texto a buscar dentro del nombre de la plantilla (sin distinguir mayusculas ni acentos).")]
        string? filtroNombre,
        CancellationToken ct) =>
        throw new NotImplementedException();
}

/// <summary>Contrato de respuesta de listar_plantillas_semanales hacia el asistente (issue #629).</summary>
public sealed record CatalogoDePlantillasSemanales(
    int Total,
    int Mostrando,
    string? Nota,
    IReadOnlyList<PlantillaResumida> Plantillas);

/// <summary>
/// Incompleta viaja solo cuando es true: null se omite en la serializacion (filtro de relevancia,
/// MEF-ADR-0047 decision 4), igual que enConstruccion en TurnoResumido.
/// </summary>
public sealed record PlantillaResumida(
    string Id,
    string Nombre,
    int Semanas,
    bool? Incompleta);
