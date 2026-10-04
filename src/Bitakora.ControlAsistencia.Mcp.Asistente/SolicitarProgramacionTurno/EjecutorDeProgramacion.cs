using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;

/// <summary>Colaborador a programar, venga del directorio o de una ficha.</summary>
public sealed record CandidatoProgramacion(
    string Identificacion,
    string CodigoColaborador,
    string NombreCompleto,
    DateOnly VigenteDesde,
    DateOnly? VigenteHasta,
    string? CodigoSede = null);

public sealed record ResultadoEjecucion(
    IReadOnlyList<ColaboradorProgramadoResumen> Programados,
    int Omitidos,
    IReadOnlyList<ColaboradorFallidoResumen>? Fallidos,
    IReadOnlyList<AvisoDeSede>? Avisos = null);

/// <summary>Conteos de la ejecucion, visibles aunque esta termine por excepcion o cancelacion.</summary>
internal sealed class ContadoresDeEjecucion
{
    public int Omitidos;
    public int Programados;
    public int Fallidos;
}

/// <summary>Lo que cada tool aporta al indicador por ejecucion.</summary>
internal sealed record DatosDeIndicador(
    string Modalidad,
    int TamanoResuelto,
    string Turno,
    string? SedeDeProgramacion,
    DateOnly Desde,
    DateOnly Hasta,
    string? Sede,
    string? Etiquetas);

internal static class IndicadorDeEjecucion
{
    private const string Plantilla =
        "Solicitud de programacion ejecutada por el asistente: {Modalidad}, {TamanoResuelto} colaboradores, "
        + "{Programados} programados, {Omitidos} omitidos, {Fallidos} fallidos, {DuracionMs} ms";

    private static readonly EventId Evento = new(1, "EjecucionSolicitudProgramacion");

    // Log estructurado a mano: [LoggerMessage] solo publica como propiedades los placeholders de
    // la plantilla, y el registro debe llevar tambien turno, sedes y ventana en customDimensions.
    public static void Emitir(
        ILogger logger, TimeProvider reloj, long inicio, DatosDeIndicador datos, ContadoresDeEjecucion contadores)
    {
        if (!logger.IsEnabled(LogLevel.Information))
            return;

        var duracionMs = (long)reloj.GetElapsedTime(inicio).TotalMilliseconds;
        List<KeyValuePair<string, object?>> propiedades =
        [
            new("Modalidad", datos.Modalidad),
            new("TamanoResuelto", datos.TamanoResuelto),
            new("Programados", contadores.Programados),
            new("Omitidos", contadores.Omitidos),
            new("Fallidos", contadores.Fallidos),
            new("DuracionMs", duracionMs),
            new("Turno", datos.Turno),
            new("SedeDeProgramacion", datos.SedeDeProgramacion),
            new("Desde", datos.Desde),
            new("Hasta", datos.Hasta),
            new("DiasVentana", (datos.Hasta.DayNumber - datos.Desde.DayNumber) + 1),
            new("Sede", datos.Sede),
            new("Etiquetas", datos.Etiquetas),
            new("{OriginalFormat}", Plantilla),
        ];

        logger.Log(
            LogLevel.Information, Evento, propiedades, null,
            (estado, _) => string.Format(
                CultureInfo.InvariantCulture,
                "Solicitud de programacion ejecutada por el asistente: {0}, {1} colaboradores, "
                + "{2} programados, {3} omitidos, {4} fallidos, {5} ms",
                estado[0].Value, estado[1].Value, estado[2].Value, estado[3].Value, estado[4].Value,
                estado[5].Value));
    }
}

// Parte comun de solicitar_programacion_turno y solicitar_programacion_turno_por_grupo (MEF-ADR-0018):
// ambas difieren solo en como obtienen los candidatos.
internal static class EjecutorDeProgramacion
{
    internal const int PostsSimultaneos = 8;

    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    private sealed record Envio(CandidatoProgramacion Entrada, TurnoAProgramar Turno, IReadOnlyList<DateOnly> Dias);

    private sealed record ResultadoEnvio(
        Envio Envio, bool Exito, IReadOnlyList<FechaRespetadaPorAusencia> Respetadas, string? Motivo);

    public static async Task<ResultadoEjecucion> EjecutarAsync(
        ProgramacionApi programacion,
        IReadOnlyList<CandidatoProgramacion> solicitados,
        AsignacionDeTurno asignacion,
        PlanDeSede planDeSede,
        VentanaDeProgramacion ventana,
        ContadoresDeEjecucion contadores,
        CancellationToken ct)
    {
        var candidatos = solicitados
            .Select(c => (Entrada: c, Dias: ventana.DiasCubiertosPor(c.VigenteDesde, c.VigenteHasta)))
            .Where(c => c.Dias.Count > 0)
            .ToList();

        var omitidos = solicitados.Count - candidatos.Count;
        contadores.Omitidos += omitidos;

        var envios = candidatos
            .SelectMany(c => c.Dias
                .GroupBy(asignacion.Para)
                .Select(g => new Envio(c.Entrada, g.Key, [.. g])))
            .ToList();

        var resultados = new ConcurrentBag<ResultadoEnvio>();
        // Los contadores avanzan durante los POST para que el indicador conserve los conteos
        // parciales si la ejecucion termina por excepcion o cancelacion; cuentan colaboradores.
        var conPostExitoso = new ConcurrentDictionary<string, byte>();
        var conPostRechazado = new ConcurrentDictionary<string, byte>();

        await Parallel.ForEachAsync(
            envios,
            new ParallelOptions { MaxDegreeOfParallelism = PostsSimultaneos, CancellationToken = ct },
            async (envio, tokenInterno) =>
            {
                var solicitud = new SolicitudProgramacionTurno(
                    Guid.CreateVersion7(),
                    envio.Turno.Id,
                    new ColaboradorSolicitado(
                        envio.Entrada.Identificacion,
                        envio.Entrada.CodigoColaborador,
                        envio.Entrada.NombreCompleto),
                    envio.Dias,
                    planDeSede.Para(envio.Entrada).Sede);

                var respuestaSolicitud = await programacion.SolicitarProgramacion(solicitud, tokenInterno);

                if (respuestaSolicitud.IsSuccessStatusCode)
                {
                    var respetadas = await LeerRespetadasAsync(respuestaSolicitud, tokenInterno);
                    resultados.Add(new ResultadoEnvio(envio, true, respetadas, null));
                    if (conPostExitoso.TryAdd(envio.Entrada.Identificacion, 0))
                        Interlocked.Increment(ref contadores.Programados);
                }
                else
                {
                    var motivo = await respuestaSolicitud.Content.ReadAsStringAsync(tokenInterno);
                    resultados.Add(new ResultadoEnvio(envio, false, [], motivo));
                    if (conPostRechazado.TryAdd(envio.Entrada.Identificacion, 0))
                        Interlocked.Increment(ref contadores.Fallidos);
                }
            });

        var programados = new List<ColaboradorProgramadoResumen>();
        var fallidos = new List<ColaboradorFallidoResumen>();
        var avisos = new List<AvisoDeSede>();

        foreach (var porColaborador in resultados.GroupBy(r => r.Envio.Entrada.Identificacion))
        {
            var entrada = porColaborador.First().Envio.Entrada;

            fallidos.AddRange(porColaborador
                .Where(r => !r.Exito)
                .OrderBy(r => r.Envio.Dias[0])
                .Select(r => new ColaboradorFallidoResumen(
                    entrada.Identificacion, r.Motivo!, asignacion.UsaUnSoloTurno ? null : r.Envio.Turno.Nombre)));

            var exitosos = porColaborador.Where(r => r.Exito).ToList();
            if (exitosos.Count == 0)
                continue;

            var diasProgramados = exitosos.SelectMany(r => r.Envio.Dias).Order().ToList();
            var conjuntoProgramados = diasProgramados.ToHashSet();
            var respetadas = exitosos
                .SelectMany(r => r.Respetadas)
                .Where(r => conjuntoProgramados.Contains(r.Fecha))
                .ToList();
            var fechasRespetadas = respetadas.Select(r => r.Fecha).ToHashSet();
            var respetados = respetadas
                .GroupBy(r => r.Motivo)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new DiasRespetadosResumen(
                    g.Key, ComprimirEnTramos(g.Select(r => r.Fecha), diasProgramados[0])))
                .ToList();

            var sedeDelCandidato = planDeSede.Para(entrada);
            programados.Add(new ColaboradorProgramadoResumen(
                entrada.Identificacion,
                entrada.NombreCompleto,
                entrada.CodigoColaborador,
                diasProgramados[0],
                diasProgramados[^1],
                diasProgramados.Count(d => !fechasRespetadas.Contains(d)),
                respetados.Count == 0 ? null : respetados,
                planDeSede.HaySedeExplicita ? null : sedeDelCandidato.Sede?.Id));
            if (sedeDelCandidato.MotivoDeAviso is { } motivoAviso
                && exitosos.Any(r => r.Envio.Turno.TieneFranjaSinSede))
                avisos.Add(new AvisoDeSede(entrada.Identificacion, motivoAviso));
        }

        return new ResultadoEjecucion(
            [.. programados.OrderBy(p => p.Identificacion, StringComparer.Ordinal)],
            omitidos,
            fallidos.Count == 0 ? null : [.. fallidos.OrderBy(f => f.Identificacion, StringComparer.Ordinal)],
            avisos.Count == 0 ? null : [.. avisos.OrderBy(a => a.Identificacion, StringComparer.Ordinal)]);
    }

    private static async Task<IReadOnlyList<FechaRespetadaPorAusencia>> LeerRespetadasAsync(
        HttpResponseMessage respuesta, CancellationToken ct)
    {
        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(cuerpo))
            return [];

        try
        {
            return JsonSerializer.Deserialize<RespuestaSolicitudCreada>(cuerpo, OpcionesLectura)
                ?.FechasRespetadas ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    // Los extremos van como dia del mes cuando caen en el mes de referencia (primer dia
    // programado del colaborador) y como yyyy-MM-dd cuando salen de el; en ese caso el tramo se
    // une con " a " porque "30-2026-10-01" no se lee como rango.
    private static string ComprimirEnTramos(IEnumerable<DateOnly> fechas, DateOnly referencia)
    {
        bool EnMesDeReferencia(DateOnly f) => f.Year == referencia.Year && f.Month == referencia.Month;
        string Formato(DateOnly f) => EnMesDeReferencia(f)
            ? f.Day.ToString(CultureInfo.InvariantCulture)
            : f.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string Rango(DateOnly primero, DateOnly ultimo) =>
            EnMesDeReferencia(primero) && EnMesDeReferencia(ultimo)
                ? $"{Formato(primero)}-{Formato(ultimo)}"
                : $"{Formato(primero)} a {Formato(ultimo)}";

        var ordenadas = fechas.Distinct().OrderBy(f => f).ToList();
        var tramos = new List<string>();
        var inicio = 0;
        for (var i = 1; i <= ordenadas.Count; i++)
        {
            if (i < ordenadas.Count && ordenadas[i].DayNumber == ordenadas[i - 1].DayNumber + 1)
                continue;

            var primero = ordenadas[inicio];
            var ultimo = ordenadas[i - 1];
            tramos.Add(primero == ultimo ? Formato(primero) : Rango(primero, ultimo));
            inicio = i;
        }

        return string.Join(", ", tramos);
    }
}
