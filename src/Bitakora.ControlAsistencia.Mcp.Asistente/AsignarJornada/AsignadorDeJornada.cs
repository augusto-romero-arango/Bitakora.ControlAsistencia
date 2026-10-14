using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AsignarJornada;

internal static class AsignadorDeJornada
{
    internal const int PutsSimultaneos = 8;

    public static async Task<(JornadaDelCatalogo? Jornada, string? Rechazo)> ValidarJornadaAsync(
        ProgramacionApi programacion, string jornadaId, CancellationToken ct)
    {
        if (!Guid.TryParse(jornadaId, out var id))
            return (null, string.Format(AsignarJornadaTool.Mensajes.JornadaIdInvalido, jornadaId));

        var respuesta = await programacion.ObtenerJornada(id, ct);
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
            return (null, string.Format(AsignarJornadaTool.Mensajes.JornadaNoExiste, jornadaId));
        if (await respuesta.LeerFalloAsync(ct) is { } fallo)
            return (null, string.Format(AsignarJornadaTool.Mensajes.RechazoDelDominio, fallo));

        var jornada = (await respuesta.Content.ReadFromJsonAsync<JornadaDelCatalogo>(ct))!;
        return jornada.Activa
            ? (jornada, null)
            : (null, string.Format(AsignarJornadaTool.Mensajes.JornadaDesactivada, jornadaId));
    }

    public static async Task<ResultadoAsignacion> AsignarAsync(
        ColaboradoresApi colaboradores, IReadOnlyList<CandidatoProgramacion> candidatos,
        JornadaDelCatalogo jornada, CancellationToken ct)
    {
        var jornadaId = Guid.Parse(jornada.JornadaId);
        var pendientes = candidatos.Where(c => c.JornadaId != jornadaId).ToList();
        var asignados = 0;
        var fallidos = new ConcurrentQueue<JornadaFallida>();

        await Parallel.ForEachAsync(
            pendientes,
            new ParallelOptions { MaxDegreeOfParallelism = PutsSimultaneos, CancellationToken = ct },
            async (candidato, token) =>
            {
                var respuesta = await colaboradores.AsignarJornada(candidato.Identificacion, jornadaId, token);
                if (await respuesta.LeerFalloAsync(token) is { } motivo)
                    fallidos.Enqueue(new JornadaFallida(candidato.Identificacion, motivo));
                else
                    Interlocked.Increment(ref asignados);
            });

        return new ResultadoAsignacion(
            asignados,
            candidatos.Count - pendientes.Count,
            [.. fallidos.OrderBy(f => f.Identificacion, StringComparer.Ordinal)]);
    }
}

internal sealed record ResultadoAsignacion(int Asignados, int YaLaTenian, IReadOnlyList<JornadaFallida> Fallidos);

public sealed record JornadaFallida(string Identificacion, string Motivo);

public sealed record JornadaAsignadaAColaboradoresResumen(
    string Resultado,
    JornadaDescritaResumen Jornada,
    string? Selector,
    int Asignados,
    [property: JsonPropertyName("ya_la_tenian")] int YaLaTenian,
    [property: JsonPropertyName("no_encontrados")] IReadOnlyList<string>? NoEncontrados,
    IReadOnlyList<JornadaFallida>? Fallidos,
    string Nota);
