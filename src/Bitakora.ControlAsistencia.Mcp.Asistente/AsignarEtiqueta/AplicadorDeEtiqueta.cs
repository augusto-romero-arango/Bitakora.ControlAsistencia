using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AsignarEtiqueta;

internal static class AplicadorDeEtiqueta
{
    internal const int EscriturasSimultaneas = 8;

    public static IReadOnlyList<string> SepararIdentificaciones(string identificaciones) =>
        [.. identificaciones.Split(',').Select(i => i.Trim()).Where(i => i.Length > 0)];

    public static IReadOnlyList<string> NoEncontradas(
        IReadOnlyList<string> solicitadas, IReadOnlyList<CandidatoProgramacion> candidatos)
    {
        var encontradas = candidatos.Select(c => c.Identificacion.Trim().ToUpperInvariant()).ToHashSet();
        return [.. solicitadas.Where(i => !encontradas.Contains(i.ToUpperInvariant()))];
    }

    public static async Task<ResultadoDeEtiqueta> AplicarAsync(
        IReadOnlyList<CandidatoProgramacion> candidatos,
        Func<string, CancellationToken, Task<HttpResponseMessage>> escribir,
        CancellationToken ct)
    {
        var exitosos = 0;
        var fallidos = new ConcurrentQueue<EtiquetaFallida>();

        await Parallel.ForEachAsync(
            candidatos,
            new ParallelOptions { MaxDegreeOfParallelism = EscriturasSimultaneas, CancellationToken = ct },
            async (candidato, token) =>
            {
                var respuesta = await escribir(candidato.Identificacion, token);
                if (await respuesta.LeerFalloAsync(token) is { } motivo)
                    fallidos.Enqueue(new EtiquetaFallida(candidato.Identificacion, motivo));
                else
                    Interlocked.Increment(ref exitosos);
            });

        return new ResultadoDeEtiqueta(
            exitosos, [.. fallidos.OrderBy(f => f.Identificacion, StringComparer.Ordinal)]);
    }
}

internal sealed record ResultadoDeEtiqueta(int Exitosos, IReadOnlyList<EtiquetaFallida> Fallidos);

public sealed record EtiquetaFallida(string Identificacion, string Motivo);

public sealed record EtiquetaAsignadaResumen(
    string Resultado,
    string Etiqueta,
    int Asignados,
    [property: JsonPropertyName("no_encontrados")] IReadOnlyList<string>? NoEncontrados,
    IReadOnlyList<EtiquetaFallida>? Fallidos,
    string Nota);

public sealed record EtiquetaRetiradaResumen(
    string Resultado,
    string Etiqueta,
    int Retirados,
    [property: JsonPropertyName("no_encontrados")] IReadOnlyList<string>? NoEncontrados,
    IReadOnlyList<EtiquetaFallida>? Fallidos,
    string Nota);
