using System.Text.Json;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;

// Atajo de arrange sobre ProgramacionApiFixture.Client (directo al Function App de Programacion),
// no sobre el cliente MCP.
public static class CatalogoDeTurnos
{
    public static readonly TimeSpan TimeoutPolling = TimeSpan.FromSeconds(30);

    /// <summary>La ficha del catalogo con ese nombre exacto, o null si todavia no se materializo.</summary>
    public static async Task<JsonDocument?> BuscarFichaAsync(
        this HttpClient programacion, string nombre, CancellationToken ct)
    {
        var texto = await programacion.GetStringAsync("/api/programacion/turnos", ct);
        using var documento = JsonDocument.Parse(texto);
        foreach (var turno in documento.RootElement.GetProperty("elementos").EnumerateArray())
            if (turno.GetProperty("nombre").GetString() == nombre)
                return JsonDocument.Parse(turno.GetRawText());

        return null;
    }

    public static Task<JsonDocument> EsperarFichaAsync(
        this HttpClient programacion, string nombre, CancellationToken ct) =>
        Polling.WaitUntilAsync(() => programacion.BuscarFichaAsync(nombre, ct), TimeoutPolling);
}
