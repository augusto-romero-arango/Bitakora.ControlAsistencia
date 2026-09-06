using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.ObtenerPlantillaSemanal;

// Tool de solo lectura sobre GET programacion/plantillas-semanales (resolver nombre -> id) + GET
// programacion/plantillas-semanales/{id} (issue #629). Remodela el cuadro compuesto de #625 como
// una linea por dia ("nombre (descripcion)") agrupada por semana con clave lunes..domingo, para
// que el asistente lo lea como tabla. El detalle con franjas de cada turno sigue siendo
// obtener_turno.
public partial class ObtenerPlantillaSemanalTool(ProgramacionApi api)
{
    internal const string NombreTool = "obtener_plantilla_semanal";

    private readonly ResolutorPlantillaPorNombre resolutor = new(api);

    [Function("ObtenerPlantillaSemanal")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Devuelve el cuadro de una plantilla semanal por su nombre exacto (mirala con "
            + "listar_plantillas_semanales): por cada semana, el turno de cada dia de lunes a "
            + "domingo con su nombre y horario, los dias sin turno y los turnos retirados o "
            + "incompletos. Para las franjas de un turno usa obtener_turno.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "plantilla",
            "Nombre exacto de la plantilla semanal del catalogo (mirala con listar_plantillas_semanales).",
            isRequired: true)]
        string plantilla,
        CancellationToken ct) =>
        throw new NotImplementedException();
}

/// <summary>Contrato de respuesta de obtener_plantilla_semanal hacia el asistente (issue #629).</summary>
public sealed record PlantillaSemanalDetallada(
    string Id,
    string Nombre,
    int Semanas,
    bool Completa,
    IReadOnlyList<SemanaDelCuadro> Cuadro);

/// <summary>
/// Una semana del cuadro con una propiedad fija por dia (lunes..domingo, serializadas en
/// minuscula por RespuestaJson) para que el asistente la lea como tabla.
/// </summary>
public sealed record SemanaDelCuadro(
    int Semana,
    string Lunes,
    string Martes,
    string Miercoles,
    string Jueves,
    string Viernes,
    string Sabado,
    string Domingo);
