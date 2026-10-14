using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;

// Las Jornadas no tienen nombre ni marca [TEST]: las del smoke se distinguen por su tope diario de
// 1 h y 0 descansos, y quedan en dev como residuo aceptado (issue #870). Como el dominio rechaza
// limites repetidos y el residuo nunca se borra, las horas semanales y el minimo diario se sortean
// al minuto: con solo seis valores posibles la suite quedaria roja tras pocas corridas.
public sealed record LimitesDeSmoke(int Horas, int Minutos, int MinutosMinimo)
{
    public static LimitesDeSmoke Sortear() =>
        new(Random.Shared.Next(1, 7), Random.Shared.Next(60), Random.Shared.Next(60));

    public LimitesDeSmoke ConOtrasHorasSemanales() => this with { Minutos = (Minutos + 30) % 60 };

    public Dictionary<string, object?> ComoArgumentos() => new()
    {
        ["horas_semanales"] = $"{Horas}:{Minutos:00}",
        ["tope_diario"] = "1:00",
        ["minimo_diario"] = $"0:{MinutosMinimo:00}",
        ["dias_descanso_por_semana"] = 0
    };
}

public static class SembradoDeJornadas
{
    private const int MaximoIntentos = 10;

    public static string Texto(CallToolResult resultado) =>
        resultado.Content.OfType<TextContentBlock>().Single().Text;

    // Un 409 por limites repetidos responde una frase, no JSON: se reintenta con otro sorteo.
    public static async Task<(JsonElement Jornada, LimitesDeSmoke Limites)> CrearAsync(McpFixture mcp, CancellationToken ct)
    {
        for (var intento = 0; intento < MaximoIntentos; intento++)
        {
            var limites = LimitesDeSmoke.Sortear();
            var resultado = await mcp.Cliente.CallToolAsync(
                "crear_jornada", limites.ComoArgumentos(), cancellationToken: ct);

            if (TryParsear(Texto(resultado), out var jornada))
                return (jornada, limites);
        }

        throw new InvalidOperationException(
            $"No se pudo crear una Jornada de smoke en {MaximoIntentos} intentos: todos los sorteos repitieron limites.");
    }

    public static async Task<JsonElement> PredeterminadaActualAsync(McpFixture mcp, CancellationToken ct)
    {
        string? cursor = null;
        do
        {
            var args = new Dictionary<string, object?>();
            if (cursor is not null)
                args["cursor"] = cursor;

            var resultado = await mcp.Cliente.CallToolAsync("listar_jornadas", args, cancellationToken: ct);
            using var json = JsonDocument.Parse(Texto(resultado));
            foreach (var j in json.RootElement.GetProperty("jornadas").EnumerateArray())
                if (j.GetProperty("es_predeterminada").GetBoolean())
                    return j.Clone();

            cursor = json.RootElement.TryGetProperty("siguienteCursor", out var c) ? c.GetString() : null;
        } while (cursor is not null);

        throw new InvalidOperationException("dev no tiene una Jornada predeterminada.");
    }

    private static bool TryParsear(string texto, out JsonElement jornada)
    {
        try
        {
            using var json = JsonDocument.Parse(texto);
            jornada = json.RootElement.Clone();
            return jornada.TryGetProperty("jornada_id", out _);
        }
        catch (JsonException)
        {
            jornada = default;
            return false;
        }
    }
}
