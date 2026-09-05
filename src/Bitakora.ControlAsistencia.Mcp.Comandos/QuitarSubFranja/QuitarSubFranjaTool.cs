using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.QuitarSubFranja;

// Cliente HTTP puro que envuelve GET programacion/turnos (resolver nombre -> id + ficha vigente
// para el eco) + POST programacion/turnos/{id}:quitar-subfranja (paso 4 MEF-ADR-0043). El
// 400/404/409 del POST se traduce a texto, nunca a excepcion (CA-ADR-0030).
public partial class QuitarSubFranjaTool(ProgramacionApi programacion)
{
    internal const string NombreTool = "quitar_subfranja";

    private readonly ResolutorTurnoPorNombre resolutor = new(programacion);

    [Function("QuitarSubFranja")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Quita de una franja ordinaria de un turno el descanso o extra que empieza a la hora "
            + "indicada. Indica el turno por su nombre, la franja por su hora de inicio, el tipo "
            + "(descanso o extra) y la hora de inicio de la sub-franja. Para corregir un horario, "
            + "quita y agrega de nuevo.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "turno", "Nombre exacto del turno del catalogo (miralo con listar_turnos).", isRequired: true)]
        string turno,
        [McpToolProperty(
            "franja", "Hora de inicio de la franja ordinaria que contiene la sub-franja, formato HH:mm.",
            isRequired: true)]
        string franja,
        [McpToolProperty("tipo", "Tipo de sub-franja: 'descanso' o 'extra'.", isRequired: true)]
        string tipo,
        [McpToolProperty("inicio", "Hora de inicio del descanso o extra a quitar, formato HH:mm.", isRequired: true)]
        string inicio,
        CancellationToken ct)
        => throw new NotImplementedException();
}

/// <summary>
/// Eco compacto de quitar_subfranja hacia el asistente: la sub-franja quitada se compone con lo
/// que mostraba la ficha vigente al momento de la llamada, en notacion compacta.
/// </summary>
public sealed record SubFranjaQuitadaResumen(
    string Resultado, string Turno, string Franja, string SubFranjaQuitada, string Nota);
