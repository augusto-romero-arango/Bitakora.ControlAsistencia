using System.Net;
using System.Text;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.SmokeTests.Seguridad;

public class SeguridadSmokeTests(McpFixture mcp)
{
    // La frontera real de solo-lectura vive en el server + key mcp_extension: los ToolAnnotations
    // y el _meta readOnlyHint son hints NO confiables segun el spec MCP. Este negativo usa
    // HttpClient crudo a proposito -- el SDK de cliente no sabe "olvidar" la key.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ServidorMcp_Responde401_CuandoElPostNoTraeLaKey()
    {
        var ct = TestContext.Current.CancellationToken;
        using var clienteSinKey = new HttpClient { BaseAddress = mcp.BaseUrl };

        using var initialize = new StringContent(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"smoke","version":"1.0"}}}""",
            Encoding.UTF8,
            "application/json");

        var respuesta = await clienteSinKey.PostAsync("/runtime/webhooks/mcp", initialize, ct);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
