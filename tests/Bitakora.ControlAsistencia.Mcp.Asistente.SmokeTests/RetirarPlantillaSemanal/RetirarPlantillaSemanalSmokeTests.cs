using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.RetirarPlantillaSemanal;

// El camino feliz (crear -> retirar) vive en CrearPlantillaSemanalSmokeTests, que siembra y limpia
// la plantilla; aqui solo el error path del .resx de la tool.
public class RetirarPlantillaSemanalSmokeTests(McpFixture mcp)
{
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarPlantillaSemanal_RespondeElMensajeDeValidacion_CuandoLaPlantillaEstaEnBlanco()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "retirar_plantilla_semanal",
            new Dictionary<string, object?> { ["plantilla"] = "   " },
            cancellationToken: ct);

        resultado.Content.OfType<TextContentBlock>().Single().Text.Should().Be("'plantilla' es obligatorio.");
    }
}
