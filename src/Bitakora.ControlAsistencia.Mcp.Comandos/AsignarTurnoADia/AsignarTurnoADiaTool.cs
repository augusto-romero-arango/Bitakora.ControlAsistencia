using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.AsignarTurnoADia;

// Cliente HTTP puro que envuelve GET programacion/plantillas-semanales (resolver plantilla) + GET
// programacion/turnos (resolver turno) + PUT programacion/plantillas-semanales/{id}/dias/{semana}/{dia}
// (paso 2 MEF-ADR-0043): correccion puntual de un solo dia sobre una plantilla ya creada
// (CA-ADR-0034 decision 4, dia = slot atomico). El 404/409 del PUT se traduce a texto, nunca a
// excepcion (CA-ADR-0030).
public partial class AsignarTurnoADiaTool(ProgramacionApi programacion)
{
    internal const string NombreTool = "asignar_turno_a_dia";

    private readonly ResolutorPlantillaPorNombre resolutorPlantilla = new(programacion);
    private readonly ResolutorTurnoPorNombre resolutorTurno = new(programacion);

    [Function("AsignarTurnoADia")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Pone o reemplaza el turno de un dia de una plantilla semanal: nombre exacto de la "
            + "plantilla (miralo con listar_plantillas_semanales), semana (1..N, 1 por defecto), "
            + "dia (lunes..domingo o 1..7) y nombre exacto del turno del catalogo (miralo con "
            + "listar_turnos). Solo admite turnos completos; el descanso es un turno mas. Si el "
            + "dia ya tenia ese mismo turno, no cambia nada.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "plantilla",
            "Nombre exacto de la plantilla semanal del catalogo (mirala con listar_plantillas_semanales).",
            isRequired: true)]
        string plantilla,
        [McpToolProperty("turno", "Nombre exacto del turno del catalogo (miralo con listar_turnos).", isRequired: true)]
        string turno,
        [McpToolProperty("dia", "Dia de la semana: lunes..domingo o 1..7.", isRequired: true)]
        string dia,
        [McpToolProperty("semana", "Semana de la plantilla (1..N). Por defecto 1.")]
        int? semana,
        CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Eco de asignar_turno_a_dia hacia el asistente: el 204 del PUT no trae body util, asi que el dia
/// se reconstruye con lo que entro a la tool y lo ya resuelto por nombre.
/// </summary>
public sealed record TurnoAsignadoResumen(string Resultado, string Plantilla, int Semana, string Dia, string Turno, string Nota);
