using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.QuitarTurnoDeDia;

// Cliente HTTP puro que envuelve GET programacion/plantillas-semanales (resolver plantilla) +
// DELETE programacion/plantillas-semanales/{id}/dias/{semana}/{dia} (paso 3 MEF-ADR-0043):
// correccion puntual de un solo dia sobre una plantilla ya creada (CA-ADR-0034 decision 4, dia =
// slot atomico). Un DELETE sobre un dia ya vacio responde 204 sin evento (idempotente,
// #622/harness#850): esta tool lo reporta como exito, no como error. El 404/409 del DELETE se
// traduce a texto, nunca a excepcion (CA-ADR-0030).
public partial class QuitarTurnoDeDiaTool(ProgramacionApi programacion, ResolutorPlantillaPorId resolutorPlantilla)
{
    internal const string NombreTool = "quitar_turno_de_dia";

    [Function("QuitarTurnoDeDia")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Deja sin turno un dia de una plantilla semanal (id de la plantilla, "
            + "semana y dia); la plantilla queda incompleta hasta que se le asigne otro con "
            + "asignar_turno_a_dia. Si el dia ya estaba vacio, no cambia nada.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "plantilla_id", "Id de la plantilla semanal, tal como lo da buscar_plantillas_semanales.", isRequired: true)]
        string plantillaId,
        [McpToolProperty("dia", "Dia de la semana: lunes..domingo o 1..7.", isRequired: true)]
        string dia,
        [McpToolProperty("semana", "Semana de la plantilla (1..N). Por defecto 1.")]
        int? semana,
        CancellationToken ct)
    {
        _ = (programacion, resolutorPlantilla);
        throw new NotImplementedException();
    }
}

/// <summary>
/// Eco de quitar_turno_de_dia hacia el asistente: el 204 del DELETE no trae body util, asi que el
/// dia se reconstruye con lo que entro a la tool y lo ya resuelto por nombre.
/// </summary>
public sealed record TurnoQuitadoResumen(string Resultado, string Plantilla, int Semana, string Dia, string Nota);
