using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.CrearTurnoDeHorarioAbierto;

public partial class CrearTurnoDeHorarioAbiertoTool(ProgramacionApi programacion)
{
    internal ProgramacionApi Programacion { get; } = programacion;

    internal const string NombreTool = "crear_turno_de_horario_abierto";

    [Function("CrearTurnoDeHorarioAbierto")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Crea un turno de horario abierto: el usuario solo dice cuantas horas se trabajan y el colaborador "
            + "entra a la hora que quiera del dia. Queda una franja que empieza a las 04:00 y dura `duracion`, "
            + "con una ventana de entrada hasta las 24:00 menos la duracion, de modo que la salida mas tardia "
            + "son las 24:00. Nace completo y programable, y se sigue disenando como cualquier turno. El nombre "
            + "es unico en el catalogo; si ya existe, responde el rechazo del dominio. Aparece en listar_turnos "
            + "en unos segundos.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty("nombre", "Nombre unico del turno en el catalogo.", isRequired: true)]
        string nombre,
        [McpToolProperty(
            "duracion",
            "Horas que se trabajan en formato H:mm, por ejemplo 6:00; mayor que 0:00 y hasta 20:00.",
            isRequired: true)]
        string duracion,
        CancellationToken ct)
        => throw new NotImplementedException();
}
