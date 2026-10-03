using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.ProgramarAusencia;

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
    public async Task<string> Run(
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
        if (string.IsNullOrWhiteSpace(identificacion))
            return string.Format(Mensajes.CampoObligatorio, "identificacion");
        if (string.IsNullOrWhiteSpace(desde))
            return string.Format(Mensajes.CampoObligatorio, "desde");
        if (string.IsNullOrWhiteSpace(hasta))
            return string.Format(Mensajes.CampoObligatorio, "hasta");
        if (string.IsNullOrWhiteSpace(motivo))
            return string.Format(Mensajes.CampoObligatorio, "motivo");

        if (!DateOnly.TryParseExact(
            desde, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fechaDesde))
            return string.Format(Mensajes.FechaInvalida, "desde", desde);
        if (!DateOnly.TryParseExact(
            hasta, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fechaHasta))
            return string.Format(Mensajes.FechaInvalida, "hasta", hasta);
        if (fechaDesde > fechaHasta)
            return Mensajes.PeriodoInvertido;

        var motivoNormalizado = MotivosValidos.FirstOrDefault(
            m => string.Equals(m, motivo.Trim(), StringComparison.OrdinalIgnoreCase));
        if (motivoNormalizado is null)
            return string.Format(Mensajes.MotivoInvalido, motivo, string.Join(", ", MotivosValidos));

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

        var inicio = fechaDesde > entrada.VigenteDesde ? fechaDesde : entrada.VigenteDesde;
        var fin = entrada.VigenteHasta is { } vigenteHasta && vigenteHasta < fechaHasta ? vigenteHasta : fechaHasta;
        if (inicio > fin)
            return string.Format(
                Mensajes.SinDiasVinculados, entrada.NombreCompleto, $"{fechaDesde:yyyy-MM-dd} a {fechaHasta:yyyy-MM-dd}");

        var respuesta = await programacion.ProgramarAusencia(
            entrada.CodigoColaborador,
            new AusenciaAProgramar(
                Guid.CreateVersion7(), entrada.Identificacion, entrada.NombreCompleto, inicio, fin, motivoNormalizado),
            ct);
        if (await respuesta.LeerFalloAsync(ct) is { } rechazo)
            return string.Format(Mensajes.RechazoDelDominio, rechazo);

        var diasFuera = Enumerable.Range(0, fechaHasta.DayNumber - fechaDesde.DayNumber + 1)
            .Select(i => fechaDesde.AddDays(i))
            .Where(dia => dia < inicio || dia > fin)
            .ToList();

        return RespuestaJson.Serializar(new AusenciaRegistradaResumen(
            Mensajes.ResultadoAusenciaRegistrada,
            entrada.NombreCompleto,
            motivoNormalizado,
            inicio,
            fin,
            diasFuera.Count == 0 ? null : diasFuera));
    }
}

public sealed record AusenciaRegistradaResumen(
    string Resultado,
    string Colaborador,
    string Motivo,
    DateOnly Desde,
    DateOnly Hasta,
    IReadOnlyList<DateOnly>? DiasFuera);
