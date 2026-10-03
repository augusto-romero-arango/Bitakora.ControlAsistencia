using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.SmokeTests.CerrarSesion;

public class CerrarSesionSmokeTests(McpFixture mcp)
{
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CerrarSesion_RespondeSinSesionYSinUrl_CuandoSeLlamaConSystemKeySinBearer()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "cerrar_sesion", new Dictionary<string, object?>(), cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        using var json = JsonDocument.Parse(resultado.Content.OfType<TextContentBlock>().Single().Text);
        var raiz = json.RootElement;

        raiz.GetProperty("resultado").GetString().Should().NotBeNullOrWhiteSpace();
        raiz.TryGetProperty("url", out _).Should().BeFalse();
    }
}
