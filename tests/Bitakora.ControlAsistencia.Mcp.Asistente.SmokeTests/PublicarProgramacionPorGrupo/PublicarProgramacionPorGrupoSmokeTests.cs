using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.PublicarProgramacionPorGrupo;

public class PublicarProgramacionPorGrupoSmokeTests(McpFixture mcp)
{
    private static readonly TimeSpan TimeoutPolling = TimeSpan.FromSeconds(30);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task PublicarProgramacionPorGrupo_AplicaElActoALosDosColaboradoresDeLaSede_CuandoElGrupoSeDefinePorSede()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigoSede = await Sembrado.RegistrarSedeAsync(mcp.Cliente, ct);
        await Sembrado.RegistrarColaboradorAsync(mcp.Cliente, codigoSede, ct);
        await Sembrado.RegistrarColaboradorAsync(mcp.Cliente, codigoSede, ct);

        var argumentos = new Dictionary<string, object?>
        {
            ["sede"] = codigoSede,
            ["desde"] = "2026-09-01",
            ["hasta"] = "2026-09-03"
        };

        using var documento = await Polling.WaitUntilAsync(
            async () =>
            {
                var respuesta = await mcp.Cliente.CallToolAsync("publicar_programacion_por_grupo", argumentos, cancellationToken: ct);
                var candidato = JsonDocument.Parse(respuesta.Content.OfType<TextContentBlock>().Single().Text);

                if (candidato.RootElement.TryGetProperty("colaboradores", out _)
                    && candidato.RootElement.GetProperty("colaboradores").GetInt32() == 2)
                    return candidato;

                candidato.Dispose();
                return null;
            },
            TimeoutPolling);

        var resultado = documento.RootElement;
        resultado.GetProperty("resultado").GetString().Should().NotBeNullOrWhiteSpace();
        resultado.GetProperty("colaboradores").GetInt32().Should().Be(2);
        resultado.TryGetProperty("fallidos", out _).Should().BeFalse();
    }
}
