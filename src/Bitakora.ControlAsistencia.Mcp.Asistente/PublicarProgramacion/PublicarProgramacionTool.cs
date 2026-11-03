using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.PublicarProgramacion;

public partial class PublicarProgramacionTool(
    ProgramacionApi programacion, ColaboradoresApi colaboradores, TimeProvider? reloj = null)
{
    internal const string NombreTool = "publicar_programacion";

    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    private const int MaximoDiasDelPeriodo = 366;

    private static readonly TimeZoneInfo ZonaBogota = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    [Function("PublicarProgramacion")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Publica el borrador de programacion de un colaborador: los dias del borrador quedan "
            + "en firme y rigen para el calculo. Los dias con ausencia siguen en el borrador. Recibe "
            + "la identificacion completa tal como la devuelve buscar_colaboradores. Con desde y hasta "
            + "(maximo 366 dias) publica los dias del borrador en ese periodo; si acabas de programar, "
            + "pasa ese mismo periodo. Sin periodo publica todo el borrador pendiente del colaborador "
            + "tal como lo registra consultar_borradores_de_programacion, que tarda unos segundos en reflejar lo "
            + "recien programado. Publicar de nuevo lo ya publicado no cambia nada. Para publicar el "
            + "borrador de un grupo usa publicar_programacion_por_grupo.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false, "idempotentHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "identificacion",
            "Identificacion completa del colaborador, tal como la devuelve buscar_colaboradores "
            + "(ej. 'CC-79879078').",
            isRequired: true)]
        string identificacion,
        [McpToolProperty("desde", "Primer dia del periodo, formato yyyy-MM-dd.", isRequired: false)]
        string? desde,
        [McpToolProperty("hasta", "Ultimo dia del periodo, formato yyyy-MM-dd.", isRequired: false)]
        string? hasta,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(identificacion))
            return string.Format(Mensajes.CampoObligatorio, "identificacion");
        if (string.IsNullOrWhiteSpace(desde) != string.IsNullOrWhiteSpace(hasta))
            return Mensajes.PeriodoIncompleto;

        DateOnly fechaDesde = default, fechaHasta = default;
        var conPeriodo = !string.IsNullOrWhiteSpace(desde);
        if (conPeriodo)
        {
            if (!DateOnly.TryParseExact(
                desde, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out fechaDesde))
                return string.Format(Mensajes.FechaInvalida, "desde", desde);
            if (!DateOnly.TryParseExact(
                hasta, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out fechaHasta))
                return string.Format(Mensajes.FechaInvalida, "hasta", hasta);
            if (fechaDesde > fechaHasta)
                return Mensajes.PeriodoInvertido;
            if (fechaHasta.DayNumber - fechaDesde.DayNumber + 1 > MaximoDiasDelPeriodo)
                return Mensajes.PeriodoExcedeElMaximo;
        }

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

        if (!conPeriodo)
        {
            var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_reloj.GetUtcNow(), ZonaBogota).DateTime);
            var respuestaVista = await programacion.ListarBorradoresDeProgramacion(hoy, [entrada.CodigoColaborador], ct);
            if (await respuestaVista.LeerFalloAsync(ct) is { } falloVista)
                return string.Format(Mensajes.RechazoDelDominio, falloVista);
            var pendientes = (await respuestaVista.Content
                .ReadFromJsonAsync<SobreDeLista<BorradorDeProgramacion>>(OpcionesLectura, ct))?.Elementos ?? [];

            var fechas = pendientes
                .Where(b => b.CodigoColaborador == entrada.CodigoColaborador)
                .SelectMany(b => b.Dias)
                .Select(d => d.Fecha)
                .ToList();
            if (fechas.Count == 0)
                return string.Format(Mensajes.SinBorradorPendiente, entrada.NombreCompleto);

            fechaDesde = fechas.Min();
            fechaHasta = fechas.Max();
        }

        foreach (var tramo in CorteEnTramos.Cortar(fechaDesde, fechaHasta, MaximoDiasDelPeriodo))
        {
            var respuesta = await programacion.PublicarProgramacion(entrada.CodigoColaborador, tramo.Desde, tramo.Hasta, ct);
            if (await respuesta.LeerFalloAsync(ct) is { } rechazo)
                return string.Format(Mensajes.RechazoDelDominio, rechazo);
        }

        return RespuestaJson.Serializar(new ProgramacionPublicadaResumen(
            Mensajes.ResultadoProgramacionPublicada, entrada.NombreCompleto, fechaDesde, fechaHasta));
    }
}

public sealed record ProgramacionPublicadaResumen(
    string Resultado, string Colaborador, DateOnly Desde, DateOnly Hasta);
