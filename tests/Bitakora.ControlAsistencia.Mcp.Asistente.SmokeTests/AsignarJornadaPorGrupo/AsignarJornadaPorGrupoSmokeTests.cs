using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.AsignarJornadaPorGrupo;

public class AsignarJornadaPorGrupoSmokeTests(McpFixture mcp)
{
    private static readonly TimeSpan TimeoutPolling = TimeSpan.FromSeconds(30);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarJornadaPorGrupo_CubreAlColaboradorDeLaSede_CuandoElGrupoSeDefinePorSede()
    {
        var ct = TestContext.Current.CancellationToken;
        var predeterminada = await SembradoDeJornadas.PredeterminadaActualAsync(mcp, ct);
        var jornadaId = predeterminada.GetProperty("jornada_id").GetString()!;
        var codigoSede = await Sembrado.RegistrarSedeAsync(mcp.Cliente, ct);
        await Sembrado.RegistrarColaboradorAsync(mcp.Cliente, codigoSede, ct);
        var argumentos = new Dictionary<string, object?>
        {
            ["jornada_id"] = jornadaId,
            ["sede"] = codigoSede
        };

        using var documento = await Polling.WaitUntilAsync(
            async () =>
            {
                var respuesta = await mcp.Cliente.CallToolAsync(
                    "asignar_jornada_por_grupo", argumentos, cancellationToken: ct);
                return TryParsear(SembradoDeJornadas.Texto(respuesta));
            },
            TimeoutPolling);

        var resultado = documento.RootElement;
        (resultado.GetProperty("asignados").GetInt32() + resultado.GetProperty("ya_la_tenian").GetInt32())
            .Should().Be(1);
        resultado.GetProperty("jornada").GetProperty("jornada_id").GetString().Should().Be(jornadaId);
        resultado.TryGetProperty("selector", out _).Should().BeTrue();
        resultado.TryGetProperty("no_encontrados", out _).Should().BeFalse();
    }

    private static JsonDocument? TryParsear(string texto)
    {
        try
        {
            var documento = JsonDocument.Parse(texto);
            if (documento.RootElement.ValueKind == JsonValueKind.Object
                && documento.RootElement.TryGetProperty("asignados", out _))
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
