using Bitakora.ControlAsistencia.Mcp.Asistente.CrearPlantilla;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.CrearPlantillaRotativa;

public partial class CrearPlantillaRotativaTool(
    ProgramacionApi programacion, ResolutorTurnoPorId resolutorPorId, ResolutorTurnoPorNombre resolutor)
{
    internal const string NombreTool = "crear_plantilla_rotativa";
    internal const int MaximoDiasDelCiclo = 42;

    [Function("CrearPlantillaRotativa")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Crea una plantilla de turnos rotativa a partir de bloques de dias consecutivos con el mismo turno, "
            + "en orden: un 6x2 son 6 dias de un turno y 2 de descanso. Cada bloque lleva dias y turno_id "
            + "(miralo con buscar_turnos) o franja (HH:mm-HH:mm, excluyentes). Para aplicarla usa "
            + "aplicar_plantilla con fecha_inicio = el primer dia del primer bloque.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty("nombre", "Nombre unico de la plantilla de turnos en el catalogo.", isRequired: true)]
        string nombre,
        [McpToolProperty(
            "bloques",
            "Bloques en orden como JSON: lista de objetos con dias y turno_id o franja. Ejemplo: "
            + """[{"dias":6,"turno_id":"<id>"},{"dias":2,"franja":"22:00-06:00"}].""",
            isRequired: true)]
        string bloques,
        [McpToolProperty("jornada_id", "Identificador de la Jornada, tal como lo da listar_jornadas.")]
        string? jornadaId,
        CancellationToken ct)
        => throw new NotImplementedException();
}

public sealed record PlantillaRotativaCreadaResumen(
    string Resultado,
    PlantillaTurnosResumen Plantilla,
    IReadOnlyList<BloqueResumen> Bloques,
    int DiasAsignados,
    IReadOnlyList<DiaDeCicloRechazado>? DiasRechazados,
    bool Completa,
    string Nota,
    IReadOnlyList<TurnoInlineResumen>? TurnosEnLinea = null,
    JornadaDescritaResumen? Jornada = null);

public sealed record BloqueResumen(string Dias, string Turno);
