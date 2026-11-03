using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.PublicarProgramacion;

public partial class PublicarProgramacionTool(ProgramacionApi programacion, ColaboradoresApi colaboradores)
{
    internal const string NombreTool = "publicar_programacion";

    private const int MaximoDiasDelPeriodo = 366;

    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    [Function("PublicarProgramacion")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Publica el borrador de programacion de un colaborador en un periodo (desde, hasta; "
            + "maximo 366 dias): los dias de ese periodo que estaban en el borrador quedan en firme "
            + "y rigen para el calculo. Los dias con ausencia siguen en el borrador. Recibe la "
            + "identificacion completa tal como la devuelve buscar_colaboradores. Publicar de nuevo "
            + "lo ya publicado no cambia nada.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false, "idempotentHint": true}""")]
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
        if (fechaHasta.DayNumber - fechaDesde.DayNumber + 1 > MaximoDiasDelPeriodo)
            return Mensajes.PeriodoExcedeElMaximo;

        var solicitada = identificacion.Trim();
        var respuestaDirectorio = await colaboradores.BuscarEnDirectorio([solicitada], 1, ct);
        if (await respuestaDirectorio.LeerFalloAsync(ct) is { } falloDirectorio)
            return string.Format(Mensajes.RechazoDelDominio, falloDirectorio);
        var directorio = (await respuestaDirectorio.Content
            .ReadFromJsonAsync<SobreDeLista<EntradaDirectorio>>(OpcionesLectura, ct))?.Elementos ?? [];

        var entrada = directorio.FirstOrDefault(e => string.Equals(
            e.Identificacion.Trim(), solicitada, StringComparison.OrdinalIgnoreCase));
        if (entrada is null)
            return string.Format(Mensajes.ColaboradorNoEncontrado, solicitada);

        var respuesta = await programacion.PublicarProgramacion(
            entrada.CodigoColaborador, fechaDesde, fechaHasta, ct);
        if (await respuesta.LeerFalloAsync(ct) is { } rechazo)
            return string.Format(Mensajes.RechazoDelDominio, rechazo);

        return RespuestaJson.Serializar(new ProgramacionPublicadaResumen(
            Mensajes.ResultadoProgramacionPublicada, entrada.NombreCompleto, fechaDesde, fechaHasta));
    }
}

public sealed record ProgramacionPublicadaResumen(
    string Resultado, string Colaborador, DateOnly Desde, DateOnly Hasta);
