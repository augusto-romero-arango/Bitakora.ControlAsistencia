using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.AgregarSubFranja;

// Cliente HTTP puro que envuelve GET programacion/turnos (resolver nombre -> id, via
// ResolutorTurnoPorNombre) + POST programacion/turnos/{id}:agregar-subfranja (paso 4
// MEF-ADR-0043). Descanso y extra comparten forma exacta: una tool con tipo, no cuatro
// (MEF-ADR-0047 decision 4). El 400/404/409 del POST se traduce a texto, nunca a excepcion
// (CA-ADR-0030).
public partial class AgregarSubFranjaTool(ProgramacionApi programacion)
{
    internal const string NombreTool = "agregar_subfranja";

    private readonly ResolutorTurnoPorNombre resolutor = new(programacion);

    [Function("AgregarSubFranja")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Agrega dentro de una franja ordinaria de un turno un descanso (pausa que se descuenta "
            + "del trabajo, ej. almuerzo) o un extra (horas suplementarias programadas). Indica el "
            + "turno por su nombre exacto, la franja por su hora de inicio y el tipo: descanso o "
            + "extra. Horas en HH:mm; si la franja es nocturna, el sistema resuelve solo si el "
            + "descanso cae despues de la medianoche. Debe quedar contenido en la franja y no "
            + "solaparse con otros descansos o extras de la misma franja.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "turno", "Nombre exacto del turno del catalogo (miralo con listar_turnos).", isRequired: true)]
        string turno,
        [McpToolProperty(
            "franja", "Hora de inicio de la franja ordinaria donde se agrega, formato HH:mm.", isRequired: true)]
        string franja,
        [McpToolProperty("tipo", "Tipo de sub-franja: 'descanso' o 'extra'.", isRequired: true)]
        string tipo,
        [McpToolProperty("inicio", "Hora de inicio del descanso o extra, formato HH:mm.", isRequired: true)]
        string inicio,
        [McpToolProperty("fin", "Hora de fin del descanso o extra, formato HH:mm.", isRequired: true)]
        string fin,
        CancellationToken ct)
        => throw new NotImplementedException();
}

/// <summary>
/// Eco compacto de agregar_subfranja hacia el asistente: el 202 del dominio no trae body, asi que
/// la sub-franja agregada se reconstruye con lo que se envio a la tool, en notacion compacta.
/// </summary>
public sealed record SubFranjaAgregadaResumen(
    string Resultado, string Turno, string Franja, string SubFranja, string Nota);
