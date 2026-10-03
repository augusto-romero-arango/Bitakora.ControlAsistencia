using System.Globalization;
using System.Net.Http.Json;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.ConsultarAusencias;

public partial class ConsultarAusenciasTool(ProgramacionApi api)
{
    internal const string NombreTool = "consultar_ausencias";
    internal const int MaximoColaboradores = 50;

    [Function("ConsultarAusencias")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Consulta quien falta en un periodo: por colaborador, sus ausencias (vacaciones, "
            + "incapacidad medica, licencia remunerada o ausencia no remunerada) con los dias que "
            + "caen en el periodo. Periodo de maximo 31 dias. Filtra opcionalmente por codigos de "
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
        if (!TryParseFecha(desde, out var desdeFecha))
            return string.Format(Mensajes.FechaInvalida, "desde", desde);

        if (!TryParseFecha(hasta, out var hastaFecha))
            return string.Format(Mensajes.FechaInvalida, "hasta", hasta);

        if (desdeFecha > hastaFecha)
            return Mensajes.DesdePosteriorAHasta;

        var respuesta = await api.ListarAusenciasDelEquipo(
            desdeFecha, hastaFecha, SepararCodigos(codigosColaborador), ct);

        var fallo = await respuesta.LeerFalloAsync(ct);
        if (fallo is not null)
            return string.Format(Mensajes.RechazoDelDominio, fallo);

        var lista = (await respuesta.Content.ReadFromJsonAsync<ListaAusenciasDelEquipo>(ct))!;

        if (lista.Colaboradores.Count == 0)
            return string.Format(Mensajes.NadieFalta, Formatear(lista.Desde), Formatear(lista.Hasta));

        var visibles = lista.Colaboradores.Take(MaximoColaboradores)
            .Select(c => new ColaboradorAusente(
                c.CodigoColaborador,
                c.NombreCompleto,
                [.. c.Ausencias.Select(a => new AusenciaCompacta(a.Motivo, [.. a.Tramos.Select(Compactar)]))]))
            .ToList();

        return RespuestaJson.Serializar(new AusenciasDelPeriodo(
            Formatear(lista.Desde),
            Formatear(lista.Hasta),
            ComponerNota(lista, visibles.Count),
            lista.Colaboradores.Count,
            visibles.Count,
            visibles));
    }

    private static string? ComponerNota(ListaAusenciasDelEquipo lista, int visibles)
    {
        var partes = new List<string>();

        if (lista.RangoRecortado)
            partes.Add(string.Format(Mensajes.NotaRecorte, Formatear(lista.Desde), Formatear(lista.Hasta)));

        if (lista.Colaboradores.Count > visibles)
            partes.Add(string.Format(
                Mensajes.NotaTruncado, visibles, lista.Colaboradores.Count, lista.Colaboradores.Count - visibles));

        return partes.Count > 0 ? string.Join(" ", partes) : null;
    }

    private static string Compactar(TramoAplicado tramo) =>
        tramo.Desde == tramo.Hasta
            ? Formatear(tramo.Desde)
            : $"{Formatear(tramo.Desde)} a {Formatear(tramo.Hasta)}";

    private static string Formatear(DateOnly fecha) =>
        fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static bool TryParseFecha(string? valor, out DateOnly fecha) =>
        DateOnly.TryParseExact(valor?.Trim() ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out fecha);

    private static IReadOnlyList<string>? SepararCodigos(string? valor) =>
        string.IsNullOrWhiteSpace(valor)
            ? null
            : [.. valor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
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
