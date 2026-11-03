using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.DescartarBorradorDeProgramacionPorGrupo;

public class DescartarBorradorDeProgramacionPorGrupoSmokeTests(McpFixture mcp)
{
    private static readonly TimeSpan TimeoutPolling = TimeSpan.FromSeconds(30);

    private async Task<JsonDocument> DescartarAsync(Dictionary<string, object?> argumentos, CancellationToken ct)
    {
        var respuesta = await mcp.Cliente.CallToolAsync(
            "descartar_borrador_de_programacion_por_grupo", argumentos, cancellationToken: ct);
        return JsonDocument.Parse(respuesta.Content.OfType<TextContentBlock>().Single().Text);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DescartarBorradorDeProgramacionPorGrupo_AplicaElActoConRangoYLeeLaVistaSinRango_CuandoElGrupoSeDefinePorSede()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigoSede = await Sembrado.RegistrarSedeAsync(mcp.Cliente, ct);
        await Sembrado.RegistrarColaboradorAsync(mcp.Cliente, codigoSede, ct);
        await Sembrado.RegistrarColaboradorAsync(mcp.Cliente, codigoSede, ct);

        // Con rango no lee la vista: colaboradores = 2 solo cuando las fichas ya materializaron a los dos.
        using var conRango = await Polling.WaitUntilAsync(
            async () =>
            {
                var candidato = await DescartarAsync(
                    new() { ["sede"] = codigoSede, ["desde"] = "2026-09-01", ["hasta"] = "2026-09-03" }, ct);
                if (candidato.RootElement.TryGetProperty("colaboradores", out var colaboradores)
                    && colaboradores.GetInt32() == 2)
                    return candidato;

                candidato.Dispose();
                return null;
            },
            TimeoutPolling);

        conRango.RootElement.TryGetProperty("fallidos", out _).Should().BeFalse();

        using var sinRango = await DescartarAsync(new() { ["sede"] = codigoSede }, ct);

        var resultado = sinRango.RootElement;
        resultado.GetProperty("resultado").GetString().Should().NotBeNullOrWhiteSpace();
        resultado.GetProperty("colaboradores").GetInt32().Should().Be(0, "los colaboradores recien sembrados no tienen borrador");
        resultado.TryGetProperty("fallidos", out _).Should().BeFalse();
    }
}
