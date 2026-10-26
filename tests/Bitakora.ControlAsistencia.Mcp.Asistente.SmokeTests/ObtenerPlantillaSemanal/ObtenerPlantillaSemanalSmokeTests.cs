using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.ObtenerPlantillaSemanal;

public class ObtenerPlantillaSemanalSmokeTests(McpFixture mcp)
{
    // Tool call real sin arrange: un id inexistente responde el mensaje en espanol, no un error
    // de protocolo.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerPlantillaSemanal_RespondeMensajeNoExiste_CuandoElIdNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var plantillaInexistente = Guid.CreateVersion7().ToString();
        var resultado = await mcp.Cliente.CallToolAsync(
            "obtener_plantilla_semanal",
            new Dictionary<string, object?> { ["plantilla_id"] = plantillaInexistente },
            cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        var texto = resultado.Content.OfType<TextContentBlock>().Single().Text;

        texto.Should().NotStartWith("{", "el mensaje de plantilla inexistente es texto, no el objeto del camino feliz");
        texto.Should().Contain(plantillaInexistente);
    }
}
