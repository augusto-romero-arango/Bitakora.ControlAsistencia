using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarMallaTurnos;

public partial class ConsultarMallaTurnosTool
{
    private readonly ControlHorasApi _controlHoras;
    private readonly ResolutorCandidatosPorGrupo _resolutor;
    private readonly TimeProvider _reloj;

    public ConsultarMallaTurnosTool(
        ControlHorasApi controlHoras, ResolutorCandidatosPorGrupo resolutor, TimeProvider reloj)
    {
        _controlHoras = controlHoras;
        _resolutor = resolutor;
        _reloj = reloj;
    }

    internal const string NombreTool = "consultar_malla_turnos";
    internal const int MaximoColaboradores = 50;
    private static readonly TimeZoneInfo ZonaBogota = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
    private static readonly CultureInfo Cultura = new("es-CO");
    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);
    private static readonly string[] DiasAbreviados = ["lun", "mar", "mié", "jue", "vie", "sáb", "dom"];

    [Function("ConsultarMallaTurnos")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Consulta la malla semanal de turnos, es decir, que le toca a cada colaborador cada dia de una "
            + "semana (lunes a domingo): turno, horario y sede, descansos, ausencias con su motivo, dias sin "
            + "programar y dias fuera de la vinculacion. Sin fecha revisa la PROXIMA semana. Filtra por sede, "
            + "etiquetas (pares categoria:valor) o codigos de colaborador. Pagina por colaborador completo: "
            + "usa cursor para la pagina siguiente. Para el detalle de bloques de un dia, usa "
            + "consultar_programacion; para lo que se sale de la Jornada, consultar_advertencias_programacion.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("fecha", "Cualquier dia de la semana a revisar, formato yyyy-MM-dd.")]
        string? fecha,
        [McpToolProperty("sede", "Codigo de la sede.")]
        string? sede,
        [McpToolProperty("etiquetas", "Pares categoria:valor separados por coma.")]
        string? etiquetas,
        [McpToolProperty("codigos_colaborador", "Codigos de colaborador separados por coma.")]
        string? codigosColaborador,
        [McpToolProperty("cursor", "El siguienteCursor de la respuesta anterior.")]
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
        var filtrosEtiqueta = FiltroEtiqueta.ParsearPares(etiquetas);

        var grupo = await _resolutor.ResolverAsync(lunes, sedeNormalizada, filtrosEtiqueta, ct);
        if (grupo.FalloDeLectura is { } falloGrupo)
            return string.Format(Mensajes.RechazoDelDominio, falloGrupo);

        var candidatos = codigosPedidos is null
            ? grupo.Candidatos.ToList()
            : [.. grupo.Candidatos.Where(c => codigosPedidos.Contains(c.CodigoColaborador, StringComparer.OrdinalIgnoreCase))];

        if (candidatos.Count == 0)
            return string.Format(
                Mensajes.GrupoSinColaboradores, DescribirCriterio(sedeNormalizada, filtrosEtiqueta, codigosPedidos), semana);

        var respuesta = await _controlHoras.ListarMallaSemanalTurnos(
            lunes,
            [.. candidatos.Select(c => new ColaboradorVigenteMalla(c.CodigoColaborador, c.VigenteDesde, c.VigenteHasta))],
            MaximoColaboradores,
            string.IsNullOrWhiteSpace(cursor) ? null : cursor,
            ct);

        if (await respuesta.LeerFalloAsync(ct) is { } fallo)
            return string.Format(Mensajes.RechazoDelDominio, fallo);

        var lista = (await respuesta.Content.ReadFromJsonAsync<ListaMallaSemanal>(OpcionesLectura, ct))!;
        var nombres = candidatos
            .GroupBy(c => c.CodigoColaborador, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().NombreCompleto, StringComparer.OrdinalIgnoreCase);

        var visibles = lista.Elementos
            .Select(f => new ColaboradorEnMalla(
                f.CodigoColaborador,
                (nombres.GetValueOrDefault(f.CodigoColaborador) ?? string.Empty).Trim(),
                [.. f.Casillas.Select(DescribirDia)]))
            .ToList();

        return RespuestaJson.Serializar(new MallaDeLaSemana(
            Formatear(lista.Desde),
            Formatear(lista.Hasta),
            visibles.Count,
            visibles,
            lista.SiguienteCursor,
            lista.SiguienteCursor is null ? null : string.Format(Mensajes.NotaMasColaboradores, lista.SiguienteCursor)));
    }

    private static string DescribirDia(CasillaMallaSemanal c)
    {
        var estado = c.Tipo switch
        {
            "Trabajo" => string.Format(
                Mensajes.DiaTrabajo,
                c.NombreTurno,
                c.HorarioResumido,
                string.Join(", ", c.Bloques.Select(b => b.NombreSede).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct())),
            "Descanso" => Mensajes.DiaDescanso,
            "Ausencia" => string.Format(Mensajes.DiaAusencia, c.MotivoAusencia),
            "FueraDeVinculacion" => Mensajes.DiaFueraDeVinculacion,
            _ => Mensajes.DiaSinProgramar
        };

        return $"{Formatear(c.Fecha)} {DiasAbreviados[((int)c.Fecha.DayOfWeek + 6) % 7]}: {estado}";
    }

    private (DateOnly Fecha, string? Error) ResolverFecha(string? fecha)
    {
        if (string.IsNullOrWhiteSpace(fecha))
        {
            var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_reloj.GetUtcNow(), ZonaBogota).DateTime);
            return (hoy.AddDays(7), null);
        }

        return DateOnly.TryParseExact(fecha.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f)
            ? (f, null)
            : (default, string.Format(Mensajes.FechaInvalida, fecha));
    }

    private static string DescribirSemana(DateOnly lunes, DateOnly domingo) =>
        lunes.Month == domingo.Month
            ? string.Format(Mensajes.SemanaMismoMes, lunes.Day, domingo.Day, Mes(domingo), domingo.Year)
            : string.Format(Mensajes.SemanaEntreMeses, lunes.Day, Mes(lunes), domingo.Day, Mes(domingo), domingo.Year);

    private static string Mes(DateOnly fecha) => fecha.ToString("MMMM", Cultura);

    private static string DescribirCriterio(
        string? sede, IReadOnlyList<FiltroEtiqueta> etiquetas, IReadOnlyList<string>? codigos)
    {
        var partes = new[]
        {
            sede is null ? null : string.Format(Mensajes.CriterioSede, sede),
            etiquetas.Count == 0 ? null : string.Format(
                Mensajes.CriterioEtiquetas, string.Join(", ", etiquetas.Select(e => $"{e.Categoria}:{e.Valor}"))),
            codigos is null ? null : string.Format(Mensajes.CriterioCodigos, string.Join(", ", codigos))
        }.Where(p => p is not null).ToList();

        return partes.Count == 0 ? Mensajes.CriterioTodos : string.Join("; ", partes);
    }

    private static string Formatear(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static IReadOnlyList<string>? Separar(string? valor) =>
        string.IsNullOrWhiteSpace(valor)
            ? null
            : [.. valor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
}

public sealed record MallaDeLaSemana(
    string Desde,
    string Hasta,
    int Mostrando,
    IReadOnlyList<ColaboradorEnMalla> Colaboradores,
    string? SiguienteCursor,
    string? Nota);

public sealed record ColaboradorEnMalla(string Codigo, string Nombre, IReadOnlyList<string> Dias);
