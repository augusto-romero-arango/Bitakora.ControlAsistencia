using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarDiasSinProgramar;

public partial class ConsultarDiasSinProgramarTool(
    ControlHorasApi controlHoras, ResolutorCandidatosPorGrupo resolutor, TimeProvider reloj)
{
    internal const string NombreTool = "consultar_dias_sin_programar";
    internal const int MaximoColaboradores = 50;
    private const int MaximoSemanas = 5;
    private static readonly TimeZoneInfo ZonaBogota = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
    private static readonly CultureInfo Cultura = new("es-CO");
    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    [Function("ConsultarDiasSinProgramar")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Consulta a quien le faltan dias por programar: que colaboradores tienen, dentro de su "
            + "vinculacion, dias sin turno, sin descanso y sin ausencia en una o varias semanas (lunes a "
            + "domingo). Sin fecha revisa la PROXIMA semana; semanas (1 a 5) amplia la ventana. Filtra por "
            + "sede, etiquetas (pares categoria:valor) o codigos de colaborador; sin filtros revisa a todos. "
            + "Solo aparecen quienes tienen dias sin programar. Para ver los turnos de alguien, usa "
            + "consultar_programacion.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("fecha", "Cualquier dia de la primera semana a revisar, formato yyyy-MM-dd. Si se omite, la proxima semana.")]
        string? fecha,
        [McpToolProperty("semanas", "Cantidad de semanas a revisar, de 1 a 5 (por defecto 1).")]
        int? semanas,
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
        var (desdeConsulta, error) = ResolverFecha(fecha);
        if (error is not null)
            return error;

        var cantidad = semanas ?? 1;
        if (cantidad is < 1 or > MaximoSemanas)
            return string.Format(Mensajes.SemanasFueraDeRango, semanas);

        var lunes = desdeConsulta.AddDays(-(((int)desdeConsulta.DayOfWeek + 6) % 7));
        var domingo = lunes.AddDays((cantidad * 7) - 1);
        var periodo = DescribirPeriodo(lunes, domingo);

        var codigosPedidos = Separar(codigosColaborador);
        var sedeNormalizada = string.IsNullOrWhiteSpace(sede) ? null : sede.Trim();
        var filtrosEtiqueta = FiltroEtiqueta.ParsearPares(etiquetas);

        var grupo = await resolutor.ResolverAsync(lunes, sedeNormalizada, filtrosEtiqueta, ct);
        if (grupo.FalloDeLectura is { } falloGrupo)
            return string.Format(Mensajes.RechazoDelDominio, falloGrupo);

        var candidatos = codigosPedidos is null
            ? grupo.Candidatos.ToList()
            : [.. grupo.Candidatos.Where(c => codigosPedidos.Contains(c.CodigoColaborador, StringComparer.OrdinalIgnoreCase))];

        var criterio = DescribirCriterio(sedeNormalizada, filtrosEtiqueta, codigosPedidos);
        if (candidatos.Count == 0)
            return string.Format(
                Mensajes.GrupoSinColaboradores, criterio.Length == 0 ? Mensajes.CriterioTodos : criterio, periodo);

        var respuesta = await controlHoras.ListarDiasSinProgramar(
            lunes,
            domingo,
            [.. candidatos.Select(c => new ColaboradorVigenteMalla(c.CodigoColaborador, c.VigenteDesde, c.VigenteHasta))],
            MaximoColaboradores,
            string.IsNullOrWhiteSpace(cursor) ? null : cursor,
            ct);

        if (await respuesta.LeerFalloAsync(ct) is { } fallo)
            return string.Format(Mensajes.RechazoDelDominio, fallo);

        var lista = (await respuesta.Content.ReadFromJsonAsync<ListaDiasSinProgramar>(OpcionesLectura, ct))!;

        if (lista.Elementos.Count == 0)
            return string.Format(Mensajes.NadieTieneDiasSinProgramar, periodo, criterio.Length == 0 ? "" : $" ({criterio})");

        var nombres = candidatos
            .GroupBy(c => c.CodigoColaborador, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().NombreCompleto.Trim(), StringComparer.OrdinalIgnoreCase);

        var visibles = lista.Elementos
            .Select(e => new ColaboradorSinProgramar(
                e.CodigoColaborador,
                nombres.GetValueOrDefault(e.CodigoColaborador, string.Empty),
                Tramos(e.DiasSinProgramar)))
            .ToList();

        return RespuestaJson.Serializar(new DiasSinProgramarDeLaVentana(
            Formatear(lista.Desde),
            Formatear(lista.Hasta),
            visibles.Count,
            visibles,
            lista.SiguienteCursor,
            lista.SiguienteCursor is null ? null : string.Format(Mensajes.NotaMasColaboradores, lista.SiguienteCursor)));
    }

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

    private static string DescribirPeriodo(DateOnly desde, DateOnly hasta) =>
        desde.Year != hasta.Year
            ? string.Format(Mensajes.SemanaEntreAnios, desde.Day, Mes(desde), desde.Year, hasta.Day, Mes(hasta), hasta.Year)
            : desde.Month == hasta.Month
                ? string.Format(Mensajes.SemanaMismoMes, desde.Day, hasta.Day, Mes(hasta), hasta.Year)
                : string.Format(Mensajes.SemanaEntreMeses, desde.Day, Mes(desde), hasta.Day, Mes(hasta), hasta.Year);

    private static string Mes(DateOnly fecha) => fecha.ToString("MMMM", Cultura);

    private static string DescribirCriterio(
        string? sede, IReadOnlyList<FiltroEtiqueta> etiquetas, IReadOnlyList<string>? codigos) =>
        string.Join("; ", new[]
        {
            sede is null ? null : string.Format(Mensajes.CriterioSede, sede),
            etiquetas.Count == 0 ? null : string.Format(
                Mensajes.CriterioEtiquetas, string.Join(", ", etiquetas.Select(e => $"{e.Categoria}:{e.Valor}"))),
            codigos is null ? null : string.Format(Mensajes.CriterioCodigos, string.Join(", ", codigos))
        }.Where(p => p is not null));

    private static string Formatear(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static IReadOnlyList<string>? Separar(string? valor) =>
        string.IsNullOrWhiteSpace(valor)
            ? null
            : [.. valor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
}

public sealed record DiasSinProgramarDeLaVentana(
    string Desde,
    string Hasta,
    int Mostrando,
    IReadOnlyList<ColaboradorSinProgramar> Colaboradores,
    string? SiguienteCursor,
    string? Nota);

public sealed record ColaboradorSinProgramar(string Codigo, string Nombre, IReadOnlyList<string> DiasSinProgramar);
