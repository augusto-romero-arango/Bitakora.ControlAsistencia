using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarMallaTurnos;

public partial class ConsultarMallaTurnosTool(
    ControlHorasApi controlHoras, ProgramacionApi programacion, ResolutorCandidatosPorGrupo resolutor, TimeProvider reloj)
{
    private readonly ProgramacionApi _programacion = programacion;

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
            + "semana (lunes a domingo): horario y sede, descansos, ausencias con su motivo, dias sin "
            + "programar y dias fuera de la vinculacion. Sin fecha revisa la PROXIMA semana. Filtra por sede, "
            + "etiquetas (pares categoria:valor) o codigos de colaborador. Pagina por colaborador completo: "
            + "usa cursor para la pagina siguiente. Para el detalle del turno y sus bloques de un dia, usa "
            + "consultar_programacion; para lo que se sale de la Jornada, consultar_advertencias_programacion. "
            + "Los dias en borrador (programados y aun sin publicar) van marcados; se ponen en vigor con publicar_programacion.")]
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

        var lunes = fechaConsulta.AddDays(-IndiceIso(fechaConsulta));
        var domingo = lunes.AddDays(6);
        var semana = DescribirSemana(lunes, domingo);

        var codigosPedidos = Separar(codigosColaborador);
        var sedeNormalizada = string.IsNullOrWhiteSpace(sede) ? null : sede.Trim();
        var filtrosEtiqueta = FiltroEtiqueta.ParsearPares(etiquetas);

        var grupo = await resolutor.ResolverAsync(lunes, sedeNormalizada, filtrosEtiqueta, ct);
        if (grupo.FalloDeLectura is { } falloGrupo)
            return string.Format(Mensajes.RechazoDelDominio, falloGrupo);

        var candidatos = codigosPedidos is null
            ? grupo.Candidatos.ToList()
            : [.. grupo.Candidatos.Where(c => codigosPedidos.Contains(c.CodigoColaborador, StringComparer.OrdinalIgnoreCase))];

        if (candidatos.Count == 0)
            return string.Format(
                Mensajes.GrupoSinColaboradores, DescribirCriterio(sedeNormalizada, filtrosEtiqueta, codigosPedidos), semana);

        var respuesta = await controlHoras.ListarMallaSemanalTurnos(
            lunes,
            [.. candidatos.Select(c => new ColaboradorVigenteMalla(c.CodigoColaborador, c.VigenteDesde, c.VigenteHasta))],
            MaximoColaboradores,
            string.IsNullOrWhiteSpace(cursor) ? null : cursor,
            ct);

        if (await respuesta.LeerFalloAsync(ct) is { } fallo)
            return string.Format(Mensajes.RechazoDelDominio, fallo);

        var lista = (await respuesta.Content.ReadFromJsonAsync<ListaMallaSemanal>(OpcionesLectura, ct))!;

        var borradores = new Dictionary<(string Codigo, DateOnly Fecha), DiaPendienteBorrador>();
        if (lista.Elementos.Count > 0)
        {
            var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), ZonaBogota).DateTime);
            var vista = await _programacion.ListarBorradoresDeProgramacion(
                hoy, [.. lista.Elementos.Select(f => f.CodigoColaborador)], lunes, domingo, ct);

            if (await vista.LeerFalloAsync(ct) is { } falloVista)
                return string.Format(Mensajes.RechazoDelDominio, falloVista);

            var pendientes = (await vista.Content.ReadFromJsonAsync<ListaBorradoresPendientes>(OpcionesLectura, ct))!;
            foreach (var colaborador in pendientes.Elementos)
                foreach (var dia in colaborador.Dias)
                    borradores[(colaborador.CodigoColaborador.ToUpperInvariant(), dia.Fecha)] = dia;
        }

        var nombres = candidatos
            .GroupBy(c => c.CodigoColaborador, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().NombreCompleto.Trim(), StringComparer.OrdinalIgnoreCase);

        var visibles = lista.Elementos
            .Select(f => new ColaboradorEnMalla(
                f.CodigoColaborador,
                nombres.GetValueOrDefault(f.CodigoColaborador, string.Empty),
                [.. f.Casillas.Select(c => DescribirDia(
                    c, borradores.GetValueOrDefault((f.CodigoColaborador.ToUpperInvariant(), c.Fecha))))]))
            .ToList();

        return RespuestaJson.Serializar(new MallaDeLaSemana(
            Formatear(lista.Desde),
            Formatear(lista.Hasta),
            visibles.Count,
            visibles,
            lista.SiguienteCursor,
            lista.SiguienteCursor is null ? null : string.Format(Mensajes.NotaMasColaboradores, lista.SiguienteCursor)));
    }

    private static string DescribirDia(CasillaMallaSemanal c, DiaPendienteBorrador? borrador)
    {
        if (borrador is not null && c.Tipo is not ("Trabajo" or "Descanso" or "Ausencia"))
            return DescribirBorrador(c.Fecha, borrador);

        var estado = c.Tipo switch
        {
            "Trabajo" when c.EsHorarioAbierto => string.Format(
                Mensajes.DiaHorarioAbierto, NotacionDuracion.Texto(MinutosOrdinarios(c)), SedesDe(c)),
            "Trabajo" => string.Format(
                Mensajes.DiaTrabajo,
                string.IsNullOrWhiteSpace(c.HorarioResumido) ? c.NombreTurno : c.HorarioResumido,
                SedesDe(c)),
            "Descanso" => Mensajes.DiaDescanso,
            "Ausencia" => string.Format(Mensajes.DiaAusencia, c.MotivoAusencia),
            "FueraDeVinculacion" => Mensajes.DiaFueraDeVinculacion,
            _ => Mensajes.DiaSinProgramar
        };

        return $"{Formatear(c.Fecha)} {DiasAbreviados[IndiceIso(c.Fecha)]}: {estado}";
    }

    private static string DescribirBorrador(DateOnly fecha, DiaPendienteBorrador d)
    {
        var estado = d.Bloques.Count == 0
            ? Mensajes.DiaDescanso
            : d.EsHorarioAbierto
                ? string.Format(Mensajes.DiaHorarioAbierto, NotacionDuracion.Texto(MinutosOrdinarios(d.Bloques)), SedesDe(d.Bloques))
                : string.Format(
                    Mensajes.DiaTrabajo,
                    string.IsNullOrWhiteSpace(d.HorarioResumido) ? d.NombreTurno : d.HorarioResumido,
                    SedesDe(d.Bloques));

        var marcado = string.Format(d.Vencido ? Mensajes.DiaEnBorradorVencido : Mensajes.DiaEnBorrador, estado);
        return $"{Formatear(fecha)} {DiasAbreviados[IndiceIso(fecha)]}: {marcado}";
    }

    private static int MinutosOrdinarios(CasillaMallaSemanal c) => MinutosOrdinarios(c.Bloques);

    private static int MinutosOrdinarios(IReadOnlyList<BloqueMallaSemanal> bloques) =>
        (int)bloques.Where(b => b.Tipo == TipoBloque.Ordinaria).Sum(b => (b.Fin - b.Inicio).TotalMinutes);

    private static string SedesDe(CasillaMallaSemanal c) => SedesDe(c.Bloques);

    private static string SedesDe(IReadOnlyList<BloqueMallaSemanal> bloques) =>
        string.Join(", ", bloques.Select(b => b.NombreSede).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct());

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

    private static int IndiceIso(DateOnly fecha) => ((int)fecha.DayOfWeek + 6) % 7;

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
