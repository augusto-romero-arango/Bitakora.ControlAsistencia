using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.SmokeTests.ObtenerSesion;

public class ObtenerSesionSmokeTests(McpFixture mcp)
{
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerSesion_ResponderTenantFijoSinCorreo_CuandoSeLlamaConSystemKeySinBearer()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "obtener_sesion", new Dictionary<string, object?>(), cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        using var json = JsonDocument.Parse(resultado.Content.OfType<TextContentBlock>().Single().Text);
        var raiz = json.RootElement;

        raiz.GetProperty("origen").GetString().Should().Be("tenant_fijo");
        raiz.TryGetProperty("correo", out _).Should().BeFalse();
    }
}
