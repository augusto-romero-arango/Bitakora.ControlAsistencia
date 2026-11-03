using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.PublicarProgramacion;

public class PublicarProgramacionSmokeTests(McpFixture mcp)
{
    private static readonly TimeSpan TimeoutPolling = TimeSpan.FromSeconds(30);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task PublicarProgramacion_Responde_CuandoElColaboradorEstaSembrado()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = Guid.CreateVersion7();
        var numeroIdentificacion = sufijo.ToString("N").ToUpperInvariant();
        var identificacion = $"CC-{numeroIdentificacion}";

        await mcp.Cliente.CallToolAsync(
            "registrar_colaborador",
            new Dictionary<string, object?>
            {
                ["tipo_identificacion"] = "CC",
                ["numero_identificacion"] = numeroIdentificacion,
                ["primer_nombre"] = "[TEST]",
                ["primer_apellido"] = "MCP",
                ["codigo_colaborador"] = $"TEST-{sufijo}",
                ["fecha_inicio"] = "2026-09-01"
            },
            cancellationToken: ct);

        var publicada = await Polling.WaitUntilAsync(
            async () =>
            {
                var respuesta = await mcp.Cliente.CallToolAsync(
                    "publicar_programacion",
                    new Dictionary<string, object?>
                    {
                        ["identificacion"] = identificacion,
                        ["desde"] = "2026-09-01",
                        ["hasta"] = "2026-09-03"
                    },
                    cancellationToken: ct);
                var candidato = JsonDocument.Parse(respuesta.Content.OfType<TextContentBlock>().Single().Text);
                if (candidato.RootElement.TryGetProperty("colaborador", out _))
                    return candidato;
                candidato.Dispose();
                return null;
            },
            TimeoutPolling);
        using var documento = publicada;

        documento.RootElement.GetProperty("resultado").GetString().Should().NotBeNullOrWhiteSpace();
        documento.RootElement.GetProperty("colaborador").GetString().Should().NotBeNullOrWhiteSpace();
    }
}
