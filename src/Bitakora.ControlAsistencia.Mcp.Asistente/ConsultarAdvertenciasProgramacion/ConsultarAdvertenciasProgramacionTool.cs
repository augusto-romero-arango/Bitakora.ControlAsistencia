using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarAdvertenciasProgramacion;

public partial class ConsultarAdvertenciasProgramacionTool(
    ControlHorasApi controlHoras, ResolutorCandidatosPorGrupo resolutor, TimeProvider reloj)
{
    internal const string NombreTool = "consultar_advertencias_programacion";
    internal const int MaximoColaboradores = 50;
    private static readonly TimeZoneInfo ZonaBogota = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
    private static readonly CultureInfo Cultura = new("es-CO");
    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    [Function("ConsultarAdvertenciasProgramacion")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Consulta las advertencias de la programacion semanal: que colaboradores tienen la "
            + "programacion de una semana (lunes a domingo) fuera de su Jornada y por cuanto -- superan "
            + "el tope diario o las horas semanales, quedan por debajo del minimo diario o de las horas "
            + "semanales, o tienen dias de descanso de mas o de menos. Sin fecha revisa la PROXIMA "
            + "semana. Filtra por sede, etiquetas (pares categoria:valor) o codigos de colaborador. "
            + "Solo aparecen quienes tienen advertencias. Para ver los turnos de alguien, usa "
            + "consultar_programacion.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("fecha", "Cualquier dia de la semana a revisar, formato yyyy-MM-dd. Si se omite, la proxima semana.")]
        string? fecha,
        [McpToolProperty("sede", "Codigo de la sede para revisar solo a sus colaboradores.")]
        string? sede,
        [McpToolProperty("etiquetas", "Pares categoria:valor separados por coma; combina todos en AND.")]
        string? etiquetas,
        [McpToolProperty("codigos_colaborador", "Codigos de colaborador separados por coma para casos puntuales.")]
        string? codigosColaborador,
        [McpToolProperty("cursor", "El siguienteCursor de la respuesta anterior, tal cual, para ver la pagina siguiente.")]
        string? cursor,
        CancellationToken ct)
    {
        var (fechaConsulta, error) = ResolverFecha(fecha);
        if (error is not null)
            return error;

        var lunes = fechaConsulta.AddDays(-(((int)fechaConsulta.DayOfWeek + 6) % 7));
        var domingo = lunes.AddDays(6);
        var semana = DescribirSemana(lunes, domingo);

        var codigosPedidos = Separar(codigosColaborador);
        var sedeNormalizada = string.IsNullOrWhiteSpace(sede) ? null : sede.Trim();
        var filtrosEtiqueta = ParsearEtiquetas(etiquetas);

        IReadOnlyList<string>? sujetos = codigosPedidos;
        if (sedeNormalizada is not null || filtrosEtiqueta.Count > 0)
        {
            var grupo = await resolutor.ResolverAsync(lunes, sedeNormalizada, filtrosEtiqueta, ct);
            if (grupo.FalloDeLectura is { } falloGrupo)
                return string.Format(Mensajes.RechazoDelDominio, falloGrupo);

            var codigosGrupo = grupo.Candidatos.Select(c => c.CodigoColaborador).ToList();
            sujetos = codigosPedidos is null
                ? codigosGrupo
                : [.. codigosGrupo.Where(c => codigosPedidos.Contains(c, StringComparer.OrdinalIgnoreCase))];

            if (sujetos.Count == 0)
                return string.Format(Mensajes.GrupoSinColaboradores, DescribirCriterio(sedeNormalizada, filtrosEtiqueta, codigosPedidos), semana);
        }

        var respuesta = await controlHoras.ListarAdvertenciasProgramacionSemanal(
            lunes, sujetos, MaximoColaboradores + 1, string.IsNullOrWhiteSpace(cursor) ? null : cursor, ct);

        if (await respuesta.LeerFalloAsync(ct) is { } fallo)
            return string.Format(Mensajes.RechazoDelDominio, fallo);

        var lista = (await respuesta.Content.ReadFromJsonAsync<ListaAdvertenciasSemana>(OpcionesLectura, ct))!;

        if (lista.Elementos.Count == 0)
        {
            var criterio = sedeNormalizada is null && filtrosEtiqueta.Count == 0 && codigosPedidos is null
                ? ""
                : $" ({DescribirCriterio(sedeNormalizada, filtrosEtiqueta, codigosPedidos)})";
            return string.Format(Mensajes.NadieTieneAdvertencias, semana, criterio);
        }

        var hayMas = lista.Elementos.Count > MaximoColaboradores;
        var visibles = lista.Elementos.Take(MaximoColaboradores).Select(Remodelar).ToList();
        var cursorSiguiente = hayMas ? lista.SiguienteCursor : null;

        return RespuestaJson.Serializar(new AdvertenciasDeLaSemana(
            Formatear(lista.Desde),
            Formatear(lista.Hasta),
            visibles.Count,
            visibles,
            cursorSiguiente,
            cursorSiguiente is null ? null : string.Format(Mensajes.NotaMasColaboradores, cursorSiguiente)));
    }

    private static ColaboradorConAdvertencias Remodelar(ElementoAdvertenciasSemana e) =>
        new(
            e.CodigoColaborador,
            e.NombreCompleto.Trim(),
            [
                .. e.Advertencias.Select(a => $"semana: {a.Descripcion}"),
                .. e.Casillas.SelectMany(c => c.Advertencias.Select(a => $"{Formatear(c.Fecha)}: {a.Descripcion}"))
            ]);

    private (DateOnly Fecha, string? Error) ResolverFecha(string? fecha)
    {
        if (string.IsNullOrWhiteSpace(fecha))
        {
            var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), ZonaBogota).DateTime);
            return (hoy.AddDays(7), null);
        }

        return DateOnly.TryParseExact(fecha.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f)
            ? (f, null)
            : (default, string.Format(Mensajes.FechaInvalida, fecha));
    }

    private static string DescribirSemana(DateOnly lunes, DateOnly domingo) =>
        lunes.Month == domingo.Month
            ? $"{lunes.Day} al {domingo.Day} de {domingo.ToString("MMMM", Cultura)} de {domingo.Year}"
            : $"{lunes.Day} de {lunes.ToString("MMMM", Cultura)} al {domingo.Day} de {domingo.ToString("MMMM", Cultura)} de {domingo.Year}";

    private static string DescribirCriterio(
        string? sede, IReadOnlyList<FiltroEtiqueta> etiquetas, IReadOnlyList<string>? codigos) =>
        string.Join("; ", new[]
        {
            sede is null ? null : $"sede {sede}",
            etiquetas.Count == 0 ? null : $"etiquetas {string.Join(", ", etiquetas.Select(e => $"{e.Categoria}:{e.Valor}"))}",
            codigos is null ? null : $"codigos {string.Join(", ", codigos)}"
        }.Where(p => p is not null));

    private static string Formatear(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static IReadOnlyList<string>? Separar(string? valor) =>
        string.IsNullOrWhiteSpace(valor)
            ? null
            : [.. valor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    private static IReadOnlyList<FiltroEtiqueta> ParsearEtiquetas(string? etiquetas) =>
        string.IsNullOrWhiteSpace(etiquetas)
            ? []
            : [.. etiquetas
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(par => par.Split(':', 2, StringSplitOptions.TrimEntries))
                .Where(partes => partes is [{ Length: > 0 }, { Length: > 0 }])
                .Select(partes => new FiltroEtiqueta(partes[0], partes[1]))];
}

public sealed record AdvertenciasDeLaSemana(
    string Desde,
    string Hasta,
    int Mostrando,
    IReadOnlyList<ColaboradorConAdvertencias> Colaboradores,
    string? SiguienteCursor,
    string? Nota);

public sealed record ColaboradorConAdvertencias(string Codigo, string Nombre, IReadOnlyList<string> Advertencias);
