using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.ConsultarProgramacion;

// UNA sola tool de rango sobre QUERY control-horas/turnos-vigentes (decision de refinamiento
// 2026-08-30 del issue #502: tools consolidadas): el caso puntual "que le toca a Juan el 3" es
// desde = hasta + codigo_colaborador, sin tool aparte sobre ObtenerTurnoVigente.
//
// Remodelado: se podan el Id (stream key "cd:...") y el HorarioResumido (los bloques compactos son
// la forma canonica y traen la sede); las fechas invalidas y el rango invertido se responden como
// mensaje sin llamar al dominio; el 422 upstream (rango que el propio dominio rechaza) se traduce
// pasando su mensaje.
public partial class ConsultarProgramacionTool(ControlHorasApi api)
{
    internal const string NombreTool = "consultar_programacion";
    internal const int MaximoDias = 50;

    [Function("ConsultarProgramacion")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Consulta que turno rige a cada colaborador en un rango de fechas (la programacion "
            + "vigente). Filtra opcionalmente por colaborador o por sede. Para un dia puntual usa "
            + "desde = hasta.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("desde", "Fecha inicial del rango, formato yyyy-MM-dd.", isRequired: true)]
        string desde,
        [McpToolProperty("hasta", "Fecha final del rango (inclusive), formato yyyy-MM-dd.", isRequired: true)]
        string hasta,
        [McpToolProperty(
            "codigo_colaborador",
            "Codigo del colaborador para ver solo su programacion; omitelo para el panorama de todos.")]
        string? codigoColaborador,
        [McpToolProperty(
            "sede_id",
            "Id de la sede para ver solo los dias con al menos un bloque en esa sede.")]
        string? sedeId,
        CancellationToken ct)
    {
        if (!TryParseFecha(desde, out var desdeFecha))
            return string.Format(Mensajes.FechaInvalida, "desde", desde);

        if (!TryParseFecha(hasta, out var hastaFecha))
            return string.Format(Mensajes.FechaInvalida, "hasta", hasta);

        if (desdeFecha > hastaFecha)
            return Mensajes.DesdePosteriorAHasta;

        var respuesta = await api.ConsultarTurnosVigentes(
            desdeFecha, hastaFecha, Normalizar(codigoColaborador), Normalizar(sedeId), ct);

        if (respuesta.StatusCode is HttpStatusCode.UnprocessableEntity or HttpStatusCode.BadRequest)
            return string.Format(Mensajes.RechazoDelDominio, await respuesta.Content.ReadAsStringAsync(ct));

        respuesta.EnsureSuccessStatusCode();

        var lista = (await respuesta.Content.ReadFromJsonAsync<ListaTurnosVigentes>(ct))!;

        var visibles = lista.Turnos.Take(MaximoDias)
            .Select(t => new DiaProgramado(
                t.CodigoColaborador,
                t.NombreCompleto,
                t.Fecha,
                t.NombreTurno.Trim(),
                [.. t.Bloques.Select(Compactar)]))
            .ToList();

        return RespuestaJson.Serializar(new ProgramacionVigente(
            lista.DesdeAplicado,
            lista.HastaAplicado,
            ComponerNota(lista, visibles.Count),
            lista.Turnos.Count,
            visibles.Count,
            visibles));
    }

    private static string? ComponerNota(ListaTurnosVigentes lista, int visibles)
    {
        var partes = new List<string>();

        if (lista.RangoRecortado)
            partes.Add(Mensajes.NotaRecorte);

        if (lista.Turnos.Count > visibles)
            partes.Add(string.Format(Mensajes.NotaTruncado, visibles, lista.Turnos.Count));

        return partes.Count > 0 ? string.Join(" ", partes) : null;
    }

    private static string Compactar(BloqueVigente bloque)
    {
        var texto = new StringBuilder();

        if (bloque.Tipo == TipoBloque.Descanso)
            texto.Append("descanso ");
        else if (bloque.Tipo == TipoBloque.Extra)
            texto.Append("extra ");

        texto.Append($"{bloque.Inicio:HH\\:mm}-{bloque.Fin:HH\\:mm}");

        var dias = (bloque.Fin.Date - bloque.Inicio.Date).Days;
        if (dias > 0)
            texto.Append($"(+{dias})");

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
/// Contrato de respuesta de consultar_programacion hacia el asistente (remodelado, issue #502).
/// Desde/Hasta son los aplicados por el dominio, que pueden diferir de los pedidos si hubo recorte
/// (la Nota lo senala).
/// </summary>
public sealed record ProgramacionVigente(
    DateOnly Desde,
    DateOnly Hasta,
    string? Nota,
    int Total,
    int Mostrando,
    IReadOnlyList<DiaProgramado> Turnos);

/// <summary>Un dia de la programacion de un colaborador, con sus bloques ya compactados.</summary>
public sealed record DiaProgramado(
    string Colaborador,
    string? Nombre,
    DateOnly Fecha,
    string Turno,
    IReadOnlyList<string> Bloques);
