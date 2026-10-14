using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.DesactivarJornada;

public class DesactivarJornadaSmokeTests(McpFixture mcp)
{
    private static readonly TimeSpan TimeoutPolling = TimeSpan.FromSeconds(30);

    private async Task<string> DesactivarAsync(string jornadaId, CancellationToken ct)
    {
        var resultado = await mcp.Cliente.CallToolAsync(
            "desactivar_jornada", new Dictionary<string, object?> { ["jornada_id"] = jornadaId }, cancellationToken: ct);
        return SembradoDeJornadas.Texto(resultado);
    }

    // El colaborador recien registrado puede no estar aun en el directorio: la respuesta trae no_encontrados.
    private static bool AsignoAUnColaborador(string texto)
    {
        try
        {
            using var json = JsonDocument.Parse(texto);
            return json.RootElement.ValueKind == JsonValueKind.Object
                && json.RootElement.TryGetProperty("asignados", out var asignados)
                && asignados.GetInt32() == 1;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DesactivarJornada_DesactivaYLuegoRespondeExitoDeNuevo_CuandoLaJornadaNoLaUsaNadie()
    {
        var ct = TestContext.Current.CancellationToken;
        var (jornada, _) = await SembradoDeJornadas.CrearAsync(mcp, ct);
        var jornadaId = jornada.GetProperty("jornada_id").GetString()!;

        var primera = await DesactivarAsync(jornadaId, ct);
        var repetida = await DesactivarAsync(jornadaId, ct);

        primera.Should().Be("Jornada desactivada.");
        repetida.Should().Be("Jornada desactivada.");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DesactivarJornada_RespondeQueLaUsaUnColaborador_CuandoSeAsignoAUnColaboradorRegistrado()
    {
        var ct = TestContext.Current.CancellationToken;
        var (jornada, _) = await SembradoDeJornadas.CrearAsync(mcp, ct);
        var jornadaId = jornada.GetProperty("jornada_id").GetString()!;
        var (identificacion, _) = await Sembrado.RegistrarColaboradorAsync(mcp.Cliente, null, ct);
        var argumentos = new Dictionary<string, object?>
        {
            ["jornada_id"] = jornadaId,
            ["identificaciones"] = identificacion
        };
        await Polling.WaitUntilAsync(
            async () =>
            {
                var texto = SembradoDeJornadas.Texto(
                    await mcp.Cliente.CallToolAsync("asignar_jornada", argumentos, cancellationToken: ct));
                return AsignoAUnColaborador(texto) ? texto : null;
            },
            TimeoutPolling);

        var frase = await Polling.WaitUntilAsync(
            async () =>
            {
                var texto = await DesactivarAsync(jornadaId, ct);
                return texto.Contains("1 colaborador vigente", StringComparison.Ordinal) ? texto : null;
            },
            TimeoutPolling);

        frase.Should().Contain("1 colaborador vigente");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DesactivarJornada_RespondeElRechazoDelServidor_CuandoLaJornadaEsLaPredeterminada()
    {
        var ct = TestContext.Current.CancellationToken;
        var predeterminada = await SembradoDeJornadas.PredeterminadaActualAsync(mcp, ct);

        var frase = await DesactivarAsync(predeterminada.GetProperty("jornada_id").GetString()!, ct);

        frase.Should().StartWith("El dominio rechazo la solicitud:");
    }
}
