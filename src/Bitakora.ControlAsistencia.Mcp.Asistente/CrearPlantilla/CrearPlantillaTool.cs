using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.CrearPlantilla;

public partial class CrearPlantillaTool(
    ProgramacionApi programacion, ResolutorTurnoPorId resolutorPorId, ResolutorTurnoPorNombre resolutor)
{
    internal const string NombreTool = "crear_plantilla";
    internal const int MinimoDiasDelCiclo = 1;
    internal const int MaximoDiasDelCiclo = 42;

    [Function("CrearPlantilla")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Crea una plantilla de turnos: un ciclo de N dias (1 a 42) que se repite, donde cada dia "
            + "lleva un turno del catalogo por su id (miralo con buscar_turnos). Solo admite turnos "
            + "completos; el descanso es un turno mas. Recibe el numero de dias del ciclo y la "
            + "composicion como JSON: una entrada por dia con dia (1..N) y turno_id o franja "
            + "(HH:mm-HH:mm, excluyentes; la franja crea o reutiliza un turno nombrado con la franja, "
            + "ej. 07:00-17:00, o 22:00-06:00+1 si cruza la medianoche; fin menor que inicio cruza la "
            + "medianoche, inicio igual a fin es 24 horas). Los dias que no listes quedan sin turno. "
            + "Si algun turno no existe, no crea nada y te dice cuales faltan. Los dias que el dominio "
            + "rechace quedan sin turno y la plantilla incompleta; corrigelos con "
            + "asignar_turno_a_dia_de_plantilla. El nombre es unico en el catalogo. Con jornada_id "
            + "(miralo con listar_jornadas) la plantilla nace asociada a esa Jornada y se audita "
            + "contra sus limites. Aparece en listar_plantillas en unos segundos. Para aplicarla usa "
            + "aplicar_plantilla.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty("nombre", "Nombre unico de la plantilla de turnos en el catalogo.", isRequired: true)]
        string nombre,
        [McpToolProperty("dias_del_ciclo", "Numero de dias del ciclo de la plantilla (1 a 42).", isRequired: true)]
        int? diasDelCiclo,
        [McpToolProperty(
            "dias",
            "Composicion de la plantilla como JSON: lista de objetos con dia (1..N) y turno_id (id que "
            + "da buscar_turnos) o franja (HH:mm-HH:mm, excluyentes). Ejemplo: "
            + """[{"dia":1,"turno_id":"<id>"},{"dia":2,"franja":"22:00-06:00"}].""",
            isRequired: true)]
        string dias,
        [McpToolProperty("jornada_id", "Identificador de la Jornada, tal como lo da listar_jornadas.")]
        string? jornadaId,
        CancellationToken ct)
    {
        _ = (programacion, resolutorPorId, resolutor);
        throw new NotImplementedException();
    }
}
