using System.Globalization;
using System.Net.Http.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarAusencias;

public partial class ConsultarAusenciasTool(ProgramacionApi api)
{
    internal const string NombreTool = "consultar_ausencias";
    internal const int MaximoColaboradores = 50;

    [Function("ConsultarAusencias")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Consulta quien falta en un periodo: por colaborador, sus ausencias (vacaciones, "
            + "incapacidad medica, licencia remunerada o ausencia no remunerada) con los dias que "
            + "caen en el periodo. Periodo de maximo 35 dias. Filtra opcionalmente por codigos de "
            + "colaborador (para tu equipo, sacalos de listar_colaboradores por sede o etiquetas). "
            + "Para un solo colaborador, pasa solo su codigo.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("desde", "Fecha inicial del periodo, formato yyyy-MM-dd.", isRequired: true)]
        string desde,
        [McpToolProperty("hasta", "Fecha final del periodo (inclusive), formato yyyy-MM-dd.", isRequired: true)]
        string hasta,
        [McpToolProperty(
            "codigos_colaborador",
            "Codigos de colaborador separados por coma para ver solo a esos; omitelo para todos los ausentes del periodo.")]
        string? codigosColaborador,
        CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}

public sealed record AusenciasDelPeriodo(
    string Desde,
    string Hasta,
    string? Nota,
    int Total,
    int Mostrando,
    IReadOnlyList<ColaboradorAusente> Colaboradores);

public sealed record ColaboradorAusente(
    string Codigo,
    string Nombre,
    IReadOnlyList<AusenciaCompacta> Ausencias);

public sealed record AusenciaCompacta(string Motivo, IReadOnlyList<string> Tramos);
