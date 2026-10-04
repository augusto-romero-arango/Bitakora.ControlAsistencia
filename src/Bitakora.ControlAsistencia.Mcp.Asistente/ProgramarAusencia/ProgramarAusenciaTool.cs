using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ProgramarAusencia;

// El recorte a la vinculacion vive aqui y no en el dominio, que no consulta el maestro de
// colaboradores (CA-ADR-0036, #330). A diferencia de la ventana de programacion, la ausencia no
// tiene tope de dias.
public partial class ProgramarAusenciaTool(ProgramacionApi programacion, ColaboradoresApi colaboradores)
{
    internal const string NombreTool = "programar_ausencia";

    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    private static readonly string[] MotivosValidos =
        ["Vacaciones", "IncapacidadMedica", "LicenciaRemunerada", "AusenciaNoRemunerada"];

    [Function("ProgramarAusencia")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Registra una ausencia de un colaborador: dias completos en que no vendra a trabajar, "
            + "con su motivo (vacaciones, incapacidad medica, licencia remunerada o ausencia no "
            + "remunerada). Recibe la identificacion completa tal como la devuelven "
            + "buscar_colaboradores o listar_colaboradores, el periodo (desde, hasta) y el motivo. "
            + "Solo registra los dias que cubre la vinculacion del colaborador y te dice cuales "
            + "quedaron fuera. Si el periodo choca con otra ausencia, no registra nada y te dice "
            + "con cual. La ausencia cubre los turnos programados de esos dias.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "identificacion",
            "Identificacion completa del colaborador, tal como la devuelve buscar_colaboradores "
            + "(ej. 'CC-79879078').",
            isRequired: true)]
        string identificacion,
        [McpToolProperty("desde", "Primer dia de la ausencia, formato yyyy-MM-dd.", isRequired: true)]
        string desde,
        [McpToolProperty("hasta", "Ultimo dia de la ausencia, formato yyyy-MM-dd.", isRequired: true)]
        string hasta,
        [McpToolProperty(
            "motivo",
            "Motivo de la ausencia: Vacaciones, IncapacidadMedica, LicenciaRemunerada o "
            + "AusenciaNoRemunerada.",
            isRequired: true)]
        string motivo,
        CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}

public sealed record AusenciaRegistradaResumen(
    string Resultado,
    string Colaborador,
    string Motivo,
    DateOnly Desde,
    DateOnly Hasta,
    IReadOnlyList<DateOnly>? DiasFuera);
