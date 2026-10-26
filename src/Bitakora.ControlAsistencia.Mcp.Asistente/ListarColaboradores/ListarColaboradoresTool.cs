using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ListarColaboradores;

// Tool consolidada de solo lectura (CA-ADR-0039 decision 2): con identificacion es Detalle
// (GET colaboradores/fichas/{id}); por sede o etiquetas es Catalogo (QUERY colaboradores/fichas).
// El catalogo pide take = tope, no tope + 1: el backend ya hace Take + 1 y arma el cursor con el
// ultimo elemento devuelto, asi que pedir uno de mas y descartarlo lo perderia en cada pagina.
//
// Sin enriquecimiento del nombre de sede (MEF-ADR-0018): codigoSede viaja tal cual. TimeProvider
// resuelve fecha_referencia a "hoy" en America/Bogota cuando el parametro llega ausente -- el back
// jamas resuelve "hoy" (decision #373), la tool MCP es el cliente que lo hace por el agente.
public partial class ListarColaboradoresTool(ColaboradoresApi api, TimeProvider reloj)
{
    internal const string NombreTool = "listar_colaboradores";
    internal const int MaximoColaboradores = 50;
    private const string FormatoFecha = "yyyy-MM-dd";
    private static readonly TimeZoneInfo ZonaBogota = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    [Function("ListarColaboradores")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Lista los colaboradores vinculados: identificacion, codigo, nombre, sede y etiquetas "
            + "de cada uno. Filtra opcionalmente por sede o por etiquetas (pares categoria:valor, "
            + "AND entre ellos). Si envias identificacion consultas la ficha puntual de ese "
            + "colaborador y los demas filtros no aplican. Sin fecha_referencia se usa el dia de "
            + "hoy. Para ver que turno le rige a alguno, pasa su codigoColaborador a "
            + "consultar_programacion. Cada colaborador trae vigenteDesde, y vigenteHasta si su "
            + "vinculacion termino: con ellos confirmas desde y hasta cuando esta vinculado.")]
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
            "Fecha desde la que se evalua la vinculacion, formato yyyy-MM-dd: devuelve a quienes "
            + "no han terminado su vinculacion en esa fecha, incluidos los que ingresan despues. "
            + "No responde quien estaba activo ese dia: para eso revisa vigenteDesde y "
            + "vigenteHasta de cada colaborador. Si se omite se usa hoy en America/Bogota.")]
        string? fechaReferencia,
        [McpToolProperty(
            "cursor",
            "Cursor opaco de la pagina siguiente del catalogo (el siguienteCursor de la respuesta "
            + "anterior); reenvialo tal cual. Se ignora si envias identificacion.")]
        string? cursor,
        CancellationToken ct)
    {
        if (Normalizar(identificacion) is { } identificacionPedida)
            return await ConsultarPuntual(identificacionPedida, ct);

        var (fecha, error) = ResolverFecha(fechaReferencia);
        if (error is not null)
            return error;

        var sedeFiltro = Normalizar(sede);
        var filtros = FiltroEtiqueta.ParsearPares(etiquetas);

        var respuesta = await api.ListarFichas(
            fecha, sedeFiltro, filtros, MaximoColaboradores, ct, cursor: Normalizar(cursor));

        if (await TraducirRechazo(respuesta, ct) is { } rechazo)
            return rechazo;

        respuesta.EnsureSuccessStatusCode();

        var sobre = await respuesta.Content.ReadFromJsonAsync<SobreDeLista<FichaColaborador>>(ct)
            ?? new SobreDeLista<FichaColaborador>([]);

        if (sobre.Elementos.Count == 0)
            return string.Format(
                Mensajes.SinColaboradores,
                fecha.ToString(FormatoFecha, CultureInfo.InvariantCulture),
                DescribirCriterio(sedeFiltro, filtros));

        var visibles = sobre.Elementos.Select(Remodelar).ToList();

        return RespuestaJson.Serializar(new CatalogoDeColaboradores(
            visibles.Count,
            visibles,
            sobre.SiguienteCursor,
            sobre.SiguienteCursor is null ? null : Mensajes.NotaHayMasColaboradores));
    }

    private static string DescribirCriterio(string? sede, IReadOnlyList<FiltroEtiqueta> etiquetas) =>
        (sede is null ? "" : $", sede {sede}")
        + (etiquetas.Count == 0
            ? ""
            : $", etiquetas {string.Join(",", etiquetas.Select(e => $"{e.Categoria}:{e.Valor}"))}");

    private async Task<string> ConsultarPuntual(string identificacion, CancellationToken ct)
    {
        var respuesta = await api.ObtenerFicha(identificacion, ct);

        if (respuesta.StatusCode == HttpStatusCode.NotFound)
            return string.Format(Mensajes.ColaboradorNoExiste, identificacion);

        if (await TraducirRechazo(respuesta, ct) is { } rechazo)
            return rechazo;

        respuesta.EnsureSuccessStatusCode();

        var ficha = (await respuesta.Content.ReadFromJsonAsync<FichaColaborador>(ct))!;

        return RespuestaJson.Serializar(Remodelar(ficha));
    }

    // Lo que el dominio rechaza por la forma del dato (una identificacion sin el prefijo de tipo,
    // un filtro en blanco) es una respuesta util para el asistente, no una excepcion: viaja como
    // mensaje con la razon del dominio -- mismo criterio que ConsultarProgramacionTool.
    private static async Task<string?> TraducirRechazo(HttpResponseMessage respuesta, CancellationToken ct) =>
        respuesta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity
            ? string.Format(Mensajes.RechazoDelDominio, await respuesta.Content.ReadAsStringAsync(ct))
            : null;

    private (DateOnly Fecha, string? Error) ResolverFecha(string? fechaReferencia)
    {
        if (string.IsNullOrWhiteSpace(fechaReferencia))
            return (DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), ZonaBogota).DateTime), null);

        return DateOnly.TryParseExact(
            fechaReferencia.Trim(), FormatoFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
            ? (fecha, null)
            : (default, string.Format(Mensajes.FechaInvalida, fechaReferencia));
    }


    private static ColaboradorFicha Remodelar(FichaColaborador ficha)
    {
        IReadOnlyList<string>? etiquetas = ficha.Etiquetas.Count > 0
            ? [.. ficha.Etiquetas.Select(e => $"{e.Categoria}:{e.Valor}")]
            : null;

        return new ColaboradorFicha(
            ficha.Id,
            ficha.NombreCompleto.Trim(),
            Normalizar(ficha.CodigoColaborador),
            ficha.CodigoSede,
            ficha.VigenteDesde,
            ficha.VigenteHasta,
            etiquetas);
    }

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

/// <summary>Catalogo de listar_colaboradores (CA-ADR-0039): sin total; SiguienteCursor y Nota solo si hay mas.</summary>
public sealed record CatalogoDeColaboradores(
    int Mostrando,
    IReadOnlyList<ColaboradorFicha> Colaboradores,
    string? SiguienteCursor,
    string? Nota);

/// <summary>
/// Colaborador remodelado token-eficiente: etiquetas en su forma original de presentacion (nunca
/// la normalizada, que es estructura interna de filtrado), sin centinela de vigencia abierta
/// (VigenteHasta ausente = vinculacion abierta) y codigoSede tal cual, sin nombre resuelto.
/// CodigoColaborador viaja porque es la llave con que consultar_programacion filtra: sin el, el
/// asistente encuentra al colaborador pero no puede encadenar la consulta siguiente.
/// </summary>
public sealed record ColaboradorFicha(
    string Identificacion,
    string Nombre,
    string? CodigoColaborador,
    string? CodigoSede,
    DateOnly VigenteDesde,
    DateOnly? VigenteHasta,
    IReadOnlyList<string>? Etiquetas);
