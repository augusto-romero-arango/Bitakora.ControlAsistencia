using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarProgramacion;

// UNA sola tool de rango sobre QUERY control-horas/turnos-vigentes (tools consolidadas):
// el caso puntual "que le toca a Juan el 3" es desde = hasta + codigos_colaborador, sin tool aparte sobre ObtenerTurnoVigente.
//
// Remodelado: se podan el Id (stream key "cd:...") y el HorarioResumido (los bloques compactos son
// la forma canonica y traen la sede); las fechas invalidas y el rango invertido se responden como
// mensaje sin llamar al dominio; el 422 upstream (rango que el propio dominio rechaza) se traduce
// pasando su mensaje.
public partial class ConsultarProgramacionTool(
    ControlHorasApi api, ResolutorCandidatosPorGrupo grupo, ResolutorSedePorCodigo sedes)
{
    internal const string NombreTool = "consultar_programacion";
    internal const int MaximoDias = 50;
    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    [Function("ConsultarProgramacion")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Consulta que turno rige a cada colaborador en un rango de fechas (la programacion "
            + "vigente). Filtra por codigos_colaborador, por etiquetas (pares categoria:valor), por sede "
            + "(codigo; los colaboradores vinculados a esa sede: que les toca a los de la sede X) o por "
            + "sede_trabajo (codigo; solo dias con algun bloque en esa sede: que se trabaja en la sede X); "
            + "los filtros se combinan en AND. Pagina de a 50 dias de un colaborador: si trae "
            + "siguienteCursor, pasalo como cursor para la pagina siguiente. Para un dia puntual usa "
            + "desde = hasta.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("desde", "Fecha inicial del rango, formato yyyy-MM-dd.", isRequired: true)]
        string desde,
        [McpToolProperty("hasta", "Fecha final del rango (inclusive), formato yyyy-MM-dd.", isRequired: true)]
        string hasta,
        [McpToolProperty("codigos_colaborador", "Codigos de colaborador separados por coma.")]
        string? codigosColaborador,
        [McpToolProperty("sede", "Codigo de la sede para ver a sus colaboradores.")]
        string? sede,
        [McpToolProperty("etiquetas", "Pares categoria:valor separados por coma.")]
        string? etiquetas,
        [McpToolProperty("sede_trabajo", "Codigo de la sede donde se trabaja.")]
        string? sedeTrabajo,
        [McpToolProperty("cursor", "El siguienteCursor de la respuesta anterior.")]
        string? cursor,
        CancellationToken ct)
    {
        if (!TryParseFecha(desde, out var fechaDesde))
            return string.Format(Mensajes.FechaInvalida, "desde", desde);
        if (!TryParseFecha(hasta, out var fechaHasta))
            return string.Format(Mensajes.FechaInvalida, "hasta", hasta);
        if (fechaDesde > fechaHasta)
            return Mensajes.DesdePosteriorAHasta;

        var periodo = $"{Formatear(fechaDesde)} a {Formatear(fechaHasta)}";
        var codigosPedidos = Separar(codigosColaborador);
        var sedeNormalizada = Normalizar(sede);
        var sedeTrabajoNormalizada = Normalizar(sedeTrabajo);
        var filtrosEtiqueta = FiltroEtiqueta.ParsearPares(etiquetas);

        IReadOnlyList<string>? sujetos = codigosPedidos;
        if (sedeNormalizada is not null || filtrosEtiqueta.Count > 0)
        {
            var resuelto = await grupo.ResolverAsync(fechaDesde, sedeNormalizada, filtrosEtiqueta, ct);
            if (resuelto.FalloDeLectura is { } falloGrupo)
                return string.Format(Mensajes.RechazoDelDominio, falloGrupo);

            var codigosGrupo = resuelto.Candidatos.Select(c => c.CodigoColaborador).ToList();
            sujetos = codigosPedidos is null
                ? codigosGrupo
                : [.. codigosGrupo.Where(c => codigosPedidos.Contains(c, StringComparer.OrdinalIgnoreCase))];

            if (sujetos.Count == 0)
                return string.Format(
                    Mensajes.GrupoSinColaboradores,
                    DescribirCriterio(sedeNormalizada, filtrosEtiqueta, codigosPedidos, null),
                    periodo);
        }

        string? sedeId = null;
        if (sedeTrabajoNormalizada is not null)
        {
            var resolucion = await sedes.ResolverIdAsync(sedeTrabajoNormalizada, ct);
            if (resolucion.FalloDeLectura is { } falloSede)
                return string.Format(Mensajes.RechazoDelDominio, falloSede);
            if (resolucion.NoExiste)
                return string.Format(Mensajes.SedeTrabajoNoExiste, sedeTrabajoNormalizada);
            sedeId = resolucion.SedeId;
        }

        var respuesta = await api.ConsultarTurnosVigentes(
            fechaDesde, fechaHasta, sujetos, sedeId, MaximoDias, Normalizar(cursor), ct);

        if (await respuesta.LeerFalloAsync(ct) is { } fallo)
            return string.Format(Mensajes.RechazoDelDominio, fallo);

        var lista = (await respuesta.Content.ReadFromJsonAsync<ListaTurnosVigentes>(OpcionesLectura, ct))!;

        if (lista.Elementos.Count == 0)
        {
            var criterio = DescribirCriterio(sedeNormalizada, filtrosEtiqueta, codigosPedidos, sedeTrabajoNormalizada);
            return string.Format(
                Mensajes.SinProgramacion,
                $"{Formatear(lista.Desde)} a {Formatear(lista.Hasta)}",
                criterio.Length == 0 ? "" : $" ({criterio})");
        }

        var dias = lista.Elementos
            .Select(t => new DiaProgramado(
                t.CodigoColaborador,
                t.NombreCompleto,
                t.Fecha,
                t.NombreTurno.Trim(),
                t.EsHorarioAbierto
                    ? [DescribirHorarioAbierto(t.Bloques)]
                    : [.. UnirCortesDeMedianoche(t.Bloques).Select(b => Compactar(b, t.Fecha))]))
            .ToList();

        var notas = new[]
        {
            lista.RangoRecortado ? Mensajes.NotaRecorte : null,
            lista.SiguienteCursor is null ? null : Mensajes.NotaMasDias
        }.Where(n => n is not null).ToList();

        return RespuestaJson.Serializar(new ProgramacionVigente(
            lista.Desde,
            lista.Hasta,
            dias.Count,
            dias,
            lista.SiguienteCursor,
            notas.Count == 0 ? null : string.Join(" ", notas)));
    }

    private static string DescribirCriterio(
        string? sede, IReadOnlyList<FiltroEtiqueta> etiquetas, IReadOnlyList<string>? codigos, string? sedeTrabajo) =>
        string.Join("; ", new[]
        {
            sede is null ? null : string.Format(Mensajes.CriterioSede, sede),
            etiquetas.Count == 0 ? null : string.Format(
                Mensajes.CriterioEtiquetas, string.Join(", ", etiquetas.Select(e => $"{e.Categoria}:{e.Valor}"))),
            codigos is null ? null : string.Format(Mensajes.CriterioCodigos, string.Join(", ", codigos)),
            sedeTrabajo is null ? null : string.Format(Mensajes.CriterioSedeTrabajo, sedeTrabajo)
        }.Where(p => p is not null));

    private static string Formatear(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static IReadOnlyList<string>? Separar(string? valor) =>
        string.IsNullOrWhiteSpace(valor)
            ? null
            : [.. valor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    private static IReadOnlyList<BloqueVigente> UnirCortesDeMedianoche(IReadOnlyList<BloqueVigente> bloques) =>
        bloques.Aggregate(new List<BloqueVigente>(), (unidos, bloque) =>
        {
            if (unidos.Count > 0 && EsCorteDeMedianoche(unidos[^1], bloque))
                unidos[^1] = unidos[^1] with { Fin = bloque.Fin };
            else
                unidos.Add(bloque);
            return unidos;
        });

    private static bool EsCorteDeMedianoche(BloqueVigente anterior, BloqueVigente siguiente) =>
        anterior.Fin == siguiente.Inicio
        && anterior.Fin.TimeOfDay == TimeSpan.Zero
        && anterior.Tipo == siguiente.Tipo
        && anterior.SedeId == siguiente.SedeId;

    private static string DescribirHorarioAbierto(IReadOnlyList<BloqueVigente> bloques)
    {
        var minutos = (int)bloques.Where(b => b.Tipo == TipoBloque.Ordinaria).Sum(b => (b.Fin - b.Inicio).TotalMinutes);
        var texto = string.Format(Mensajes.HorarioAbierto, TextoDeDuracion(minutos / 60, minutos % 60));
        var sede = bloques.Select(b => b.NombreSede ?? b.SedeId).FirstOrDefault(s => s is not null);
        return sede is null ? texto : $"{texto}, sede: {sede}";
    }

    private static string TextoDeDuracion(int horas, int minutos) => (horas, minutos) switch
    {
        (_, 0) => $"{horas} {Mensajes.Horas}",
        (0, _) => $"{minutos} {Mensajes.Minutos}",
        _ => $"{horas} {Mensajes.Horas} {minutos} {Mensajes.Minutos}",
    };

    private static string Compactar(BloqueVigente bloque, DateOnly fecha)
    {
        var texto = new StringBuilder();

        if (bloque.Tipo == TipoBloque.Descanso)
            texto.Append("descanso ");
        else if (bloque.Tipo == TipoBloque.Extra)
            texto.Append("extra ");

        texto.Append(NotacionFranja.Rango(
            TimeOnly.FromDateTime(bloque.Inicio),
            TimeOnly.FromDateTime(bloque.Fin),
            DateOnly.FromDateTime(bloque.Inicio).DayNumber - fecha.DayNumber,
            DateOnly.FromDateTime(bloque.Fin).DayNumber - fecha.DayNumber));

        var sede = bloque.NombreSede ?? bloque.SedeId;
        if (sede is not null)
            texto.Append($", sede: {sede}");

        return texto.ToString();
    }

    private static bool TryParseFecha(string valor, out DateOnly fecha) =>
        DateOnly.TryParseExact(valor?.Trim() ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out fecha);

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

/// <summary>
/// Contrato de respuesta de consultar_programacion hacia el asistente (ventana temporal, CA-ADR-0039).
/// Desde/Hasta son los aplicados por el dominio. Sin total.
/// </summary>
public sealed record ProgramacionVigente(
    DateOnly Desde,
    DateOnly Hasta,
    int Mostrando,
    IReadOnlyList<DiaProgramado> Dias,
    string? SiguienteCursor,
    string? Nota);

/// <summary>Un dia de la programacion de un colaborador, con sus bloques ya compactados.</summary>
public sealed record DiaProgramado(
    string Colaborador,
    string? Nombre,
    DateOnly Fecha,
    string Turno,
    IReadOnlyList<string> Bloques);
