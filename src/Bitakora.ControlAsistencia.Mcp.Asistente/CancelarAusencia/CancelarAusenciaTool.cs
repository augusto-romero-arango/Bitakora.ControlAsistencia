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
    public async Task<string> Run(
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
        if (string.IsNullOrWhiteSpace(identificacion))
            return string.Format(Mensajes.CampoObligatorio, "identificacion");
        if (string.IsNullOrWhiteSpace(desde))
            return string.Format(Mensajes.CampoObligatorio, "desde");
        if (string.IsNullOrWhiteSpace(hasta))
            return string.Format(Mensajes.CampoObligatorio, "hasta");

        if (!DateOnly.TryParseExact(
            desde, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fechaDesde))
            return string.Format(Mensajes.FechaInvalida, "desde", desde);
        if (!DateOnly.TryParseExact(
            hasta, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fechaHasta))
            return string.Format(Mensajes.FechaInvalida, "hasta", hasta);
        if (fechaDesde > fechaHasta)
            return Mensajes.PeriodoInvertido;

        var solicitada = identificacion.Trim();
        var respuestaDirectorio = await colaboradores.BuscarEnDirectorio([solicitada], 1, ct);
        if (await respuestaDirectorio.LeerFalloAsync(ct) is { } falloDirectorio)
            return string.Format(Mensajes.RechazoDelDominio, falloDirectorio);
        var directorio = await respuestaDirectorio.Content
            .ReadFromJsonAsync<List<EntradaDirectorio>>(OpcionesLectura, ct) ?? [];

        var entrada = directorio.FirstOrDefault(e => string.Equals(
            e.Identificacion.Trim(), solicitada, StringComparison.OrdinalIgnoreCase));
        if (entrada is null)
            return string.Format(Mensajes.ColaboradorNoEncontrado, solicitada);

        var respuestaAusencias = await programacion.ListarAusenciasColaborador(
            entrada.CodigoColaborador, fechaDesde, fechaHasta, ct);
        if (await respuestaAusencias.LeerFalloAsync(ct) is { } falloAusencias)
            return string.Format(Mensajes.RechazoDelDominio, falloAusencias);
        var ausencias = await respuestaAusencias.Content
            .ReadFromJsonAsync<List<AusenciaListada>>(OpcionesLectura, ct) ?? [];

        var porCancelar = ausencias
            .Select(a => (Ausencia: a, Fechas: DiasDe(a.TramosVigentes, fechaDesde, fechaHasta)))
            .Where(x => x.Fechas.Count > 0)
            .Select(x => completa == true
                ? x with { Fechas = DiasDe(x.Ausencia.TramosVigentes, DateOnly.MinValue, DateOnly.MaxValue) }
                : x)
            .ToList();

        if (porCancelar.Count == 0)
            return string.Format(
                Mensajes.SinAusenciasEnElPeriodo, entrada.NombreCompleto, $"{fechaDesde:yyyy-MM-dd} a {fechaHasta:yyyy-MM-dd}");

        var canceladas = new List<AusenciaCanceladaResumen>();
        foreach (var (ausencia, fechas) in porCancelar)
        {
            var respuesta = await programacion.CancelarAusencia(
                entrada.CodigoColaborador, ausencia.Id, fechas, ct);
            if (await respuesta.LeerFalloAsync(ct) is { } rechazo)
                return string.Format(Mensajes.RechazoDelDominio, rechazo);

            canceladas.Add(new AusenciaCanceladaResumen(ausencia.Motivo, fechas[0], fechas[^1], fechas.Count));
        }

        return RespuestaJson.Serializar(new AusenciasCanceladasResumen(
            Mensajes.ResultadoAusenciasCanceladas, entrada.NombreCompleto, canceladas));
    }

    private static List<DateOnly> DiasDe(IReadOnlyList<TramoAusencia> tramos, DateOnly desde, DateOnly hasta) =>
        [.. tramos
            .Select(t => (Inicio: t.Desde > desde ? t.Desde : desde, Fin: t.Hasta < hasta ? t.Hasta : hasta))
            .Where(t => t.Inicio <= t.Fin)
            .SelectMany(t => Enumerable.Range(0, t.Fin.DayNumber - t.Inicio.DayNumber + 1)
                .Select(i => t.Inicio.AddDays(i)))
            .Distinct()
            .Order()];
}

public sealed record AusenciasCanceladasResumen(
    string Resultado, string Colaborador, IReadOnlyList<AusenciaCanceladaResumen> Canceladas);

public sealed record AusenciaCanceladaResumen(string Motivo, DateOnly Desde, DateOnly Hasta, int Dias);
