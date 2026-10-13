using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;

// Las Jornadas no tienen nombre ni marca [TEST]: las del smoke se distinguen por su tope diario de
// 1 h y 0 descansos, y quedan en dev como residuo aceptado (issue #870).
public static class SembradoDeJornadas
{
    public static string Texto(CallToolResult resultado) =>
        resultado.Content.OfType<TextContentBlock>().Single().Text;

    // Un 409 por limites repetidos responde una frase, no JSON: se reintenta con otra N.
    public static async Task<(JsonElement Jornada, int N)> CrearAsync(McpFixture mcp, CancellationToken ct)
    {
        foreach (var n in Enumerable.Range(1, 6).OrderBy(_ => Random.Shared.Next()))
        {
            var resultado = await mcp.Cliente.CallToolAsync(
                "crear_jornada",
                new Dictionary<string, object?>
                {
                    ["horas_semanales"] = $"{n}:00",
                    ["tope_diario"] = "1:00",
                    ["minimo_diario"] = "0:00",
                    ["dias_descanso_por_semana"] = 0
                },
                cancellationToken: ct);

            if (TryParsear(Texto(resultado), out var jornada))
                return (jornada, n);
        }

        throw new InvalidOperationException("No se pudo crear una Jornada de smoke: las seis N ya existen.");
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
