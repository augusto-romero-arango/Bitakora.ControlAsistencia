using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.AsignarJornada;

public class AsignarJornadaSmokeTests(McpFixture mcp)
{
    private static readonly TimeSpan TimeoutPolling = TimeSpan.FromSeconds(30);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarJornada_AsignaLaJornadaYLuegoLaReconoceYaAsignada_CuandoSeRepiteConUnColaboradorRegistrado()
    {
        var ct = TestContext.Current.CancellationToken;
        var predeterminada = await SembradoDeJornadas.PredeterminadaActualAsync(mcp, ct);
        var jornadaId = predeterminada.GetProperty("jornada_id").GetString()!;
        var (identificacion, _) = await Sembrado.RegistrarColaboradorAsync(mcp.Cliente, null, ct);
        var argumentos = new Dictionary<string, object?>
        {
            ["jornada_id"] = jornadaId,
            ["identificaciones"] = identificacion
        };

        using var primera = await Polling.WaitUntilAsync(
            async () =>
            {
                var respuesta = await mcp.Cliente.CallToolAsync("asignar_jornada", argumentos, cancellationToken: ct);
                return TryParsear(SembradoDeJornadas.Texto(respuesta), "asignados");
            },
            TimeoutPolling);

        primera.RootElement.GetProperty("asignados").GetInt32().Should().Be(1);
        primera.RootElement.GetProperty("jornada").GetProperty("jornada_id").GetString().Should().Be(jornadaId);
        primera.RootElement.TryGetProperty("fallidos", out _).Should().BeFalse();

        using var repetida = await Polling.WaitUntilAsync(
            async () =>
            {
                var respuesta = await mcp.Cliente.CallToolAsync("asignar_jornada", argumentos, cancellationToken: ct);
                var candidato = TryParsear(SembradoDeJornadas.Texto(respuesta), "ya_la_tenian");
                if (candidato?.RootElement.GetProperty("ya_la_tenian").GetInt32() == 1)
                    return candidato;

                candidato?.Dispose();
                return null;
            },
            TimeoutPolling);

        repetida.RootElement.GetProperty("ya_la_tenian").GetInt32().Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarJornada_RespondeQueLaJornadaNoExiste_CuandoElIdentificadorEsUnGuidInexistente()
    {
        var ct = TestContext.Current.CancellationToken;
        var inexistente = Guid.CreateVersion7().ToString();

        var resultado = await mcp.Cliente.CallToolAsync(
            "asignar_jornada",
            new Dictionary<string, object?> { ["jornada_id"] = inexistente, ["identificaciones"] = "CC-1" },
            cancellationToken: ct);

        SembradoDeJornadas.Texto(resultado).Should().Be($"La Jornada '{inexistente}' no existe; revisa listar_jornadas.");
    }

    private static JsonDocument? TryParsear(string texto, string campoEsperado)
    {
        try
        {
            var documento = JsonDocument.Parse(texto);
            if (documento.RootElement.ValueKind == JsonValueKind.Object
                && documento.RootElement.TryGetProperty(campoEsperado, out _))
                return documento;

            documento.Dispose();
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
