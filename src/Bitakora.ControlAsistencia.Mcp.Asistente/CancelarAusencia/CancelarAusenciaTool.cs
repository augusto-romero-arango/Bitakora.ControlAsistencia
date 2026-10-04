using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.CancelarAusencia;

public partial class CancelarAusenciaTool(ProgramacionApi programacion, ColaboradoresApi colaboradores)
{
    internal const string NombreTool = "cancelar_ausencia";

    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    [Function("CancelarAusencia")]
    public Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Cancela ausencias de un colaborador en un periodo. Sin completa, cancela solo los "
            + "dias del periodo de cualquier ausencia que caiga ahi (ej. 'volvio el 20': desde el "
            + "20 hasta el fin de la ausencia). Con completa en true, cancela enteras las "
            + "ausencias que toquen el periodo. Al cancelar, el dia vuelve al turno que tenia "
            + "programado. Te dice que cancelo.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "identificacion",
            "Identificacion completa del colaborador, tal como la devuelve buscar_colaboradores "
            + "(ej. 'CC-79879078').",
            isRequired: true)]
        string identificacion,
        [McpToolProperty("desde", "Primer dia del periodo, formato yyyy-MM-dd.", isRequired: true)]
        string desde,
        [McpToolProperty("hasta", "Ultimo dia del periodo, formato yyyy-MM-dd.", isRequired: true)]
        string hasta,
        [McpToolProperty(
            "completa",
            "Si es true, cancela enteras las ausencias que toquen el periodo; por defecto false.",
            isRequired: false)]
        bool? completa,
        CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}

public sealed record AusenciasCanceladasResumen(
    string Resultado, string Colaborador, IReadOnlyList<AusenciaCanceladaResumen> Canceladas);

public sealed record AusenciaCanceladaResumen(string Motivo, DateOnly Desde, DateOnly Hasta, int Dias);
