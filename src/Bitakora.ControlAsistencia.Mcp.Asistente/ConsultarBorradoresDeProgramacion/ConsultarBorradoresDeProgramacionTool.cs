using System.Globalization;
using System.Text.Json;
using System.Net.Http.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarBorradoresDeProgramacion;

public partial class ConsultarBorradoresDeProgramacionTool(
    ProgramacionApi api, ResolutorCandidatosPorGrupo resolutor, TimeProvider reloj)
{
    internal const string NombreTool = "consultar_borradores_de_programacion";
    internal const int MaximoColaboradores = 50;

    private static readonly TimeZoneInfo ZonaBogota = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    [Function("ConsultarBorradoresDeProgramacion")]
    public async Task<string> Run(
        [McpToolTrigger(NombreTool, "Consulta que falta publicar: por colaborador, los dias de su borrador de programacion, "
            + "lo programado que todavia no rige para el calculo. Sin periodo muestra todo lo pendiente; "
            + "con desde y hasta, solo los dias de ese periodo. Filtra opcionalmente por sede, por "
            + "etiquetas (pares categoria:valor) o por codigos de colaborador; si combinas, se "
            + "intersectan. Muestra hasta 50 colaboradores por pagina; si hay mas, la respuesta trae "
            + "siguienteCursor: repite con cursor para ver la siguiente pagina. Lo recien programado "
            + "tarda unos segundos en aparecer. Para ponerlo en vigor usa publicar_programacion y para "
            + "tirarlo descartar_borrador_de_programacion, con la identificacion de cada colaborador. "
            + "Para ver el turno de cada dia, usa consultar_malla_turnos.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("desde", "Fecha inicial del periodo, formato yyyy-MM-dd.")]
        string? desde,
        [McpToolProperty("hasta", "Fecha final del periodo (inclusive), formato yyyy-MM-dd.")]
        string? hasta,
        [McpToolProperty("sede", "Codigo de la sede para ver solo a sus colaboradores.")]
        string? sede,
        [McpToolProperty("etiquetas", "Pares categoria:valor separados por coma; combina todos en AND.")]
        string? etiquetas,
        [McpToolProperty("codigos_colaborador", "Codigos de colaborador separados por coma.")]
        string? codigosColaborador,
        [McpToolProperty("cursor", "El siguienteCursor de la respuesta anterior, tal cual.")]
        string? cursor,
        CancellationToken ct)
    {
        var hayDesde = !string.IsNullOrWhiteSpace(desde);
        var hayHasta = !string.IsNullOrWhiteSpace(hasta);
        if (hayDesde != hayHasta)
            return Mensajes.RangoIncompleto;

        DateOnly desdeFecha = default, hastaFecha = default;
        if (hayDesde)
        {
            if (!TryParseFecha(desde, out desdeFecha))
                return string.Format(Mensajes.FechaInvalida, "desde", desde);
            if (!TryParseFecha(hasta, out hastaFecha))
                return string.Format(Mensajes.FechaInvalida, "hasta", hasta);
            if (desdeFecha > hastaFecha)
                return Mensajes.DesdePosteriorAHasta;
        }

        var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), ZonaBogota).DateTime);
        var codigosPedidos = SepararCodigos(codigosColaborador);
        var sedeNormalizada = string.IsNullOrWhiteSpace(sede) ? null : sede.Trim();
        var filtrosEtiqueta = FiltroEtiqueta.ParsearPares(etiquetas);
        var criterio = DescribirCriterio(sedeNormalizada, filtrosEtiqueta, codigosPedidos);

        IReadOnlyList<string>? sujetos = codigosPedidos;
        if (sedeNormalizada is not null || filtrosEtiqueta.Count > 0)
        {
            var grupo = await resolutor.ResolverAsync(hayDesde ? desdeFecha : hoy, sedeNormalizada, filtrosEtiqueta, ct);
            if (grupo.FalloDeLectura is { } falloGrupo)
                return string.Format(Mensajes.RechazoDelDominio, falloGrupo);

            var codigosGrupo = grupo.Candidatos.Select(c => c.CodigoColaborador).ToList();
            sujetos = codigosPedidos is null
                ? codigosGrupo
                : [.. codigosGrupo.Where(c => codigosPedidos.Contains(c, StringComparer.OrdinalIgnoreCase))];

            if (sujetos.Count == 0)
                return string.Format(Mensajes.GrupoSinColaboradores, criterio);
        }

        var respuesta = await api.ListarBorradoresDeProgramacion(
            hoy, sujetos,
            hayDesde ? desdeFecha : null, hayHasta ? hastaFecha : null,
            MaximoColaboradores, string.IsNullOrWhiteSpace(cursor) ? null : cursor, ct);

        var fallo = await respuesta.LeerFalloAsync(ct);
        if (fallo is not null)
            return string.Format(Mensajes.RechazoDelDominio, fallo);

        var lista = (await respuesta.Content
            .ReadFromJsonAsync<SobreDeLista<BorradorDeProgramacion>>(OpcionesLectura, ct))!;

        if (lista.Elementos.Count == 0)
            return string.Format(
                Mensajes.SinBorradores,
                criterio.Length == 0 ? "" : $" ({criterio})",
                hayDesde ? $" entre {Formatear(desdeFecha)} y {Formatear(hastaFecha)}" : "");

        var visibles = lista.Elementos
            .Select(b => new ColaboradorConBorrador(
                b.CodigoColaborador, b.Identificacion, b.NombreCompleto, Tramos([.. b.Dias.Select(d => d.Fecha)])))
            .ToList();

        return RespuestaJson.Serializar(new BorradoresPendientes(
            hayDesde ? Formatear(desdeFecha) : null,
            hayDesde ? Formatear(hastaFecha) : null,
            visibles.Count,
            visibles,
            lista.SiguienteCursor,
            lista.SiguienteCursor is null ? null : Mensajes.NotaMasColaboradores));
    }

    private static string DescribirCriterio(
        string? sede, IReadOnlyList<FiltroEtiqueta> etiquetas, IReadOnlyList<string>? codigos) =>
        string.Join("; ", new[]
        {
            sede is null ? null : string.Format(Mensajes.CriterioSede, sede),
            etiquetas.Count == 0 ? null : string.Format(
                Mensajes.CriterioEtiquetas, string.Join(", ", etiquetas.Select(e => $"{e.Categoria}:{e.Valor}"))),
            codigos is null ? null : string.Format(Mensajes.CriterioCodigos, string.Join(", ", codigos))
        }.Where(p => p is not null));

    private static IReadOnlyList<string> Tramos(IReadOnlyList<DateOnly> dias)
    {
        var tramos = new List<string>();
        var ordenados = dias.Distinct().Order().ToList();
        var i = 0;
        while (i < ordenados.Count)
        {
            var j = i;
            while (j + 1 < ordenados.Count && ordenados[j + 1] == ordenados[j].AddDays(1))
                j++;
            tramos.Add(j == i
                ? Formatear(ordenados[i])
                : $"{Formatear(ordenados[i])} a {Formatear(ordenados[j])}");
            i = j + 1;
        }

        return tramos;
    }

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

public sealed record BorradoresPendientes(
    string? Desde,
    string? Hasta,
    int Mostrando,
    IReadOnlyList<ColaboradorConBorrador> Colaboradores,
    string? SiguienteCursor,
    string? Nota);

public sealed record ColaboradorConBorrador(
    string Codigo,
    string? Identificacion,
    string Nombre,
    IReadOnlyList<string> Dias);
