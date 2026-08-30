using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.SmokeTests.ConsultarProgramacion;

public class ConsultarProgramacionSmokeTests(McpFixture mcp)
{
    // Error path que NO toca los dominios: la validacion de fecha corta en el worker y responde
    // el mensaje del .resx en produccion. Afirmar el texto exacto prueba que los recursos
    // embebidos viajaron en el publish (un GetString nulo o un resx ausente daria otro texto).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ConsultarProgramacion_RespondeElMensajeDeValidacion_CuandoLaFechaEsInvalida()
    {
        var ct = TestContext.Current.CancellationToken;
        var resultado = await mcp.Cliente.CallToolAsync(
            "consultar_programacion",
            new Dictionary<string, object?> { ["desde"] = "2026-99-99", ["hasta"] = "2026-01-01" },
            cancellationToken: ct);

        resultado.Content.OfType<TextContentBlock>().Single().Text
            .Should().Be("'desde' debe ser una fecha con formato yyyy-MM-dd; llego '2026-99-99'.");
    }
}
