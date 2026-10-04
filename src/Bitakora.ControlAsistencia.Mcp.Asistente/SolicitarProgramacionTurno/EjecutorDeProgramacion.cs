using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;

/// <summary>Colaborador a programar, venga del directorio o de una ficha.</summary>
public sealed record CandidatoProgramacion(
    string Identificacion,
    string CodigoColaborador,
    string NombreCompleto,
    DateOnly VigenteDesde,
    DateOnly? VigenteHasta);

public sealed record ResultadoEjecucion(
    IReadOnlyList<ColaboradorProgramadoResumen> Programados,
    int Omitidos,
    IReadOnlyList<ColaboradorFallidoResumen>? Fallidos);

// Parte comun de solicitar_programacion_turno y solicitar_programacion_turno_por_grupo (MEF-ADR-0018):
// ambas difieren solo en como obtienen los candidatos.
internal static class EjecutorDeProgramacion
{
    internal const int PostsSimultaneos = 8;

    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    public static async Task<ResultadoEjecucion> EjecutarAsync(
        ProgramacionApi programacion,
        IReadOnlyList<CandidatoProgramacion> solicitados,
        Guid turnoId,
        SedeProgramada sedeProgramada,
        VentanaDeProgramacion ventana,
        CancellationToken ct)
    {
        var candidatos = solicitados
            .Select(c => (Entrada: c, Dias: ventana.DiasCubiertosPor(c.VigenteDesde, c.VigenteHasta)))
            .Where(c => c.Dias.Count > 0)
            .ToList();

        var omitidos = solicitados.Count - candidatos.Count;

        var programados = new ConcurrentBag<ColaboradorProgramadoResumen>();
        var fallidos = new ConcurrentBag<ColaboradorFallidoResumen>();

        await Parallel.ForEachAsync(
            candidatos,
            new ParallelOptions { MaxDegreeOfParallelism = PostsSimultaneos, CancellationToken = ct },
            async (candidato, tokenInterno) =>
            {
                var solicitud = new SolicitudProgramacionTurno(
                    Guid.CreateVersion7(),
                    turnoId,
                    new ColaboradorSolicitado(
                        candidato.Entrada.Identificacion,
                        candidato.Entrada.CodigoColaborador,
                        candidato.Entrada.NombreCompleto),
                    candidato.Dias,
                    sedeProgramada);

                var respuestaSolicitud = await programacion.SolicitarProgramacion(solicitud, tokenInterno);

                if (respuestaSolicitud.IsSuccessStatusCode)
                {
                    var respetadas = await LeerRespetadasAsync(respuestaSolicitud, tokenInterno);
                    var fechasRespetadas = respetadas.Select(r => r.Fecha).ToHashSet();
                    var diasProgramados = candidato.Dias.Count(d => !fechasRespetadas.Contains(d));
                    var respetados = respetadas
                        .Where(r => candidato.Dias.Contains(r.Fecha))
                        .GroupBy(r => r.Motivo)
                        .OrderBy(g => g.Key, StringComparer.Ordinal)
                        .Select(g => new DiasRespetadosResumen(
                            g.Key, ComprimirEnTramos(g.Select(r => r.Fecha), candidato.Dias[0])))
                        .ToList();

                    programados.Add(new ColaboradorProgramadoResumen(
                        candidato.Entrada.Identificacion,
                        candidato.Entrada.NombreCompleto,
                        candidato.Entrada.CodigoColaborador,
                        candidato.Dias[0],
                        candidato.Dias[^1],
                        diasProgramados,
                        respetados.Count == 0 ? null : respetados));
                }
                else
                {
                    var motivo = await respuestaSolicitud.Content.ReadAsStringAsync(tokenInterno);
                    fallidos.Add(new ColaboradorFallidoResumen(candidato.Entrada.Identificacion, motivo));
                }
            });

        return new ResultadoEjecucion(
            [.. programados.OrderBy(p => p.Identificacion, StringComparer.Ordinal)],
            omitidos,
            fallidos.IsEmpty ? null : [.. fallidos.OrderBy(f => f.Identificacion, StringComparer.Ordinal)]);
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
