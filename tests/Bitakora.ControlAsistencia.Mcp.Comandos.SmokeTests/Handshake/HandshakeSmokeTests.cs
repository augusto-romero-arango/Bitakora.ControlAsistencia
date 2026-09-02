using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.Handshake;

public class HandshakeSmokeTests(McpFixture mcp)
{
    // El initialize ya ocurrio dentro de McpClient.CreateAsync (fixture); que el nombre coincida
    // con el serverName de host.json prueba que el host cargo la extension MCP y NUESTRO
    // host.json, no un default.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ServidorMcp_ReportaSuIdentidad_CuandoCompletaElHandshake()
    {
        var ct = TestContext.Current.CancellationToken;
        await mcp.Cliente.PingAsync(cancellationToken: ct);

        mcp.Cliente.ServerInfo.Name.Should().Be("ControlAsistencias Comandos");
    }
}
