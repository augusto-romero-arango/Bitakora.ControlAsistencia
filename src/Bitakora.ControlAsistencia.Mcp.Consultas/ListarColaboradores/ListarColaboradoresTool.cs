using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.ListarColaboradores;

// Tool de solo lectura sobre QUERY colaboradores/fichas (listado) y GET colaboradores/fichas/{id}
// (consulta puntual) -- issue #530, cierre de la cadena colaborador+sede que #502 dejo fuera de
// alcance. Tool consolidada (decision de refinamiento del issue): identificacion? decide la ruta,
// no hay tool separada para el caso puntual.
//
// Sin enriquecimiento del nombre de sede (MEF-ADR-0018): codigoSede viaja tal cual. TimeProvider
// resuelve fecha_referencia a "hoy" en America/Bogota cuando el parametro llega ausente -- el back
// jamas resuelve "hoy" (decision #373), la tool MCP es el cliente que lo hace por el agente.
public partial class ListarColaboradoresTool(ColaboradoresApi api, TimeProvider reloj)
{
    internal const string NombreTool = "listar_colaboradores";
    internal const int MaximoColaboradores = 20;
    internal const int TakeUpstream = 200;
    private const string FormatoFecha = "yyyy-MM-dd";
    private static readonly TimeZoneInfo ZonaBogota = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    [Function("ListarColaboradores")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Lista los colaboradores vinculados: identificacion, nombre, sede y etiquetas de cada "
            + "uno. Filtra opcionalmente por sede o por etiquetas (pares categoria:valor, AND entre "
            + "ellos). Si envias identificacion consultas la ficha puntual de ese colaborador y los "
            + "demas filtros no aplican. Sin fecha_referencia se usa el dia de hoy.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "identificacion",
            "Identificacion puntual del colaborador (ej. 'CC-123456'). Si se envia, sede, "
            + "etiquetas y fecha_referencia se ignoran y se responde la ficha unica.")]
        string? identificacion,
        [McpToolProperty("sede", "Codigo de la sede para ver solo sus colaboradores vinculados.")]
        string? sede,
        [McpToolProperty(
            "etiquetas",
            "Pares categoria:valor separados por coma (ej. 'area:tecnologia,turno:diurno'); "
            + "combina todos en AND.")]
        string? etiquetas,
        [McpToolProperty(
            "fecha_referencia",
            "Fecha de vigencia a evaluar, formato yyyy-MM-dd. Si se omite se usa hoy en "
            + "America/Bogota.")]
        string? fechaReferencia,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(identificacion))
            return await ConsultarPuntual(identificacion, ct);

        var (fecha, error) = ResolverFecha(fechaReferencia);
        if (error is not null)
            return error;

        var respuesta = await api.ListarFichas(
            fecha, sede, ParsearEtiquetas(etiquetas), TakeUpstream, ct);
        respuesta.EnsureSuccessStatusCode();

        var fichas = await respuesta.Content.ReadFromJsonAsync<IReadOnlyList<FichaColaborador>>(ct)
            ?? [];

        var visibles = fichas.Take(MaximoColaboradores).Select(Remodelar).ToList();

        var nota = fichas.Count > visibles.Count
            ? string.Format(Mensajes.NotaTruncado, visibles.Count, fichas.Count)
            : null;

        return RespuestaJson.Serializar(new CatalogoDeColaboradores(fichas.Count, visibles.Count, nota, visibles));
    }

    private async Task<string> ConsultarPuntual(string identificacion, CancellationToken ct)
    {
        var respuesta = await api.ObtenerFicha(identificacion, ct);

        if (respuesta.StatusCode == HttpStatusCode.NotFound)
            return string.Format(Mensajes.ColaboradorNoExiste, identificacion);

        respuesta.EnsureSuccessStatusCode();

        var ficha = (await respuesta.Content.ReadFromJsonAsync<FichaColaborador>(ct))!;

        return RespuestaJson.Serializar(Remodelar(ficha));
    }

    private (DateOnly Fecha, string? Error) ResolverFecha(string? fechaReferencia)
    {
        if (string.IsNullOrWhiteSpace(fechaReferencia))
            return (DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), ZonaBogota).DateTime), null);

        return DateOnly.TryParseExact(
            fechaReferencia, FormatoFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
            ? (fecha, null)
            : (default, string.Format(Mensajes.FechaInvalida, fechaReferencia));
    }

    private static IReadOnlyList<FiltroEtiqueta> ParsearEtiquetas(string? etiquetas) =>
        string.IsNullOrWhiteSpace(etiquetas)
            ? []
            : [.. etiquetas
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(par => par.Split(':', 2))
                .Where(partes => partes.Length == 2)
                .Select(partes => new FiltroEtiqueta(partes[0], partes[1]))];

    private static ColaboradorFicha Remodelar(FichaColaborador ficha)
    {
        var etiquetas = ficha.EtiquetasNormalizadas.Count > 0
            ? (IReadOnlyList<string>)[.. ficha.EtiquetasNormalizadas.Select(par => $"{par.Key}:{par.Value}")]
            : null;

        return new ColaboradorFicha(
            ficha.Id,
            ficha.NombreCompleto.Trim(),
            ficha.CodigoSede,
            ficha.VigenteDesde,
            ficha.VigenteHasta,
            etiquetas);
    }
}

/// <summary>Contrato de respuesta de listar_colaboradores hacia el asistente (remodelado, issue #530).</summary>
public sealed record CatalogoDeColaboradores(
    int Total,
    int Mostrando,
    string? Nota,
    IReadOnlyList<ColaboradorFicha> Colaboradores);

/// <summary>
/// Colaborador remodelado token-eficiente: sin EtiquetasNormalizadas ni centinela de vigencia
/// abierta (VigenteHasta null = vinculacion abierta), codigoSede tal cual (sin nombre resuelto).
/// </summary>
public sealed record ColaboradorFicha(
    string Identificacion,
    string Nombre,
    string? CodigoSede,
    DateOnly VigenteDesde,
    DateOnly? VigenteHasta,
    IReadOnlyList<string>? Etiquetas);
