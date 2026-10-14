using System.Globalization;
using System.Net.Http.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarAusencias;

public partial class ConsultarAusenciasTool(ProgramacionApi api, ResolutorCandidatosPorGrupo resolutor)
{
    internal const string NombreTool = "consultar_ausencias";
    internal const int MaximoColaboradores = 50;

    [Function("ConsultarAusencias")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Consulta quien falta en un periodo: por colaborador, sus ausencias (vacaciones, "
            + "incapacidad medica, licencia remunerada o ausencia no remunerada) con los dias que "
            + "caen en el periodo. Periodo de maximo 35 dias. Filtra opcionalmente por sede, por "
            + "etiquetas (pares categoria:valor) o por codigos de colaborador; si combinas, se "
            + "intersectan. Para un solo colaborador, pasa solo su codigo. Muestra hasta 50 "
            + "colaboradores por pagina; si hay mas, la respuesta trae siguienteCursor: repite con "
            + "cursor para ver la siguiente pagina.")]
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
        [McpToolProperty("sede", "Codigo de la sede para ver solo a sus colaboradores.")]
        string? sede,
        [McpToolProperty("etiquetas", "Pares categoria:valor separados por coma; combina todos en AND.")]
        string? etiquetas,
        [McpToolProperty("cursor", "El siguienteCursor de la respuesta anterior, tal cual, para ver la pagina siguiente.")]
        string? cursor,
        CancellationToken ct)
    {
        if (!TryParseFecha(desde, out var desdeFecha))
            return string.Format(Mensajes.FechaInvalida, "desde", desde);

        if (!TryParseFecha(hasta, out var hastaFecha))
            return string.Format(Mensajes.FechaInvalida, "hasta", hasta);

        if (desdeFecha > hastaFecha)
            return Mensajes.DesdePosteriorAHasta;

        var codigosPedidos = SepararCodigos(codigosColaborador);
        var sedeNormalizada = string.IsNullOrWhiteSpace(sede) ? null : sede.Trim();
        var filtrosEtiqueta = FiltroEtiqueta.ParsearPares(etiquetas);
        var criterio = DescribirCriterio(sedeNormalizada, filtrosEtiqueta, codigosPedidos);

        IReadOnlyList<string>? sujetos = codigosPedidos;
        if (sedeNormalizada is not null || filtrosEtiqueta.Count > 0)
        {
            var grupo = await resolutor.ResolverAsync(desdeFecha, sedeNormalizada, filtrosEtiqueta, ct);
            if (grupo.FalloDeLectura is { } falloGrupo)
                return string.Format(Mensajes.RechazoDelDominio, falloGrupo);

            var codigosGrupo = grupo.Candidatos.Select(c => c.CodigoColaborador).ToList();
            sujetos = codigosPedidos is null
                ? codigosGrupo
                : [.. codigosGrupo.Where(c => codigosPedidos.Contains(c, StringComparer.OrdinalIgnoreCase))];

            if (sujetos.Count == 0)
                return string.Format(
                    Mensajes.GrupoSinColaboradores, criterio, Formatear(desdeFecha), Formatear(hastaFecha));
        }

        var respuesta = await api.ListarAusenciasDelEquipo(
            desdeFecha, hastaFecha, sujetos, MaximoColaboradores,
            string.IsNullOrWhiteSpace(cursor) ? null : cursor, ct);

        var fallo = await respuesta.LeerFalloAsync(ct);
        if (fallo is not null)
            return string.Format(Mensajes.RechazoDelDominio, fallo);

        var lista = (await respuesta.Content.ReadFromJsonAsync<ListaAusenciasDelEquipo>(ct))!;

        if (lista.Elementos.Count == 0)
            return string.Format(
                Mensajes.NadieFalta, Formatear(lista.Desde), Formatear(lista.Hasta),
                criterio.Length == 0 ? "" : $" ({criterio})");

        var visibles = lista.Elementos
            .Select(c => new ColaboradorAusente(
                c.CodigoColaborador,
                c.NombreCompleto,
                [.. c.Ausencias.Select(a => new AusenciaCompacta(a.Motivo, [.. a.Tramos.Select(Compactar)]))]))
            .ToList();

        return RespuestaJson.Serializar(new AusenciasDelPeriodo(
            Formatear(lista.Desde),
            Formatear(lista.Hasta),
            visibles.Count,
            visibles,
            lista.SiguienteCursor,
            ComponerNota(lista)));
    }

    private static string? ComponerNota(ListaAusenciasDelEquipo lista)
    {
        var partes = new List<string>();

        if (lista.RangoRecortado)
            partes.Add(string.Format(Mensajes.NotaRecorte, Formatear(lista.Desde), Formatear(lista.Hasta)));

        if (lista.SiguienteCursor is not null)
            partes.Add(string.Format(Mensajes.NotaMasColaboradores, lista.SiguienteCursor));

        return partes.Count > 0 ? string.Join(" ", partes) : null;
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
    int Mostrando,
    IReadOnlyList<ColaboradorAusente> Colaboradores,
    string? SiguienteCursor,
    string? Nota);

public sealed record ColaboradorAusente(
    string Codigo,
    string Nombre,
    IReadOnlyList<AusenciaCompacta> Ausencias);

public sealed record AusenciaCompacta(string Motivo, IReadOnlyList<string> Tramos);
