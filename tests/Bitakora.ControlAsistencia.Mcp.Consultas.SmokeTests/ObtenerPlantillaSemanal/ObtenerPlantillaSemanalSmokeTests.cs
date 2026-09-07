using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.SmokeTests.ObtenerPlantillaSemanal;

public class ObtenerPlantillaSemanalSmokeTests(McpFixture mcp)
{
    // CA-5 (issue #629): tool call real sin arrange -- el camino feliz (crear plantilla + 1 dia)
    // queda cubierto por el smoke de #627 desde Comandos, ya que esta suite no cuenta con un
    // arrange sobre Programacion. Aqui se confirma que un nombre inexistente responde el mensaje
    // en espanol, no un error de protocolo.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerPlantillaSemanal_RespondeMensajeNoExiste_CuandoElNombreNoEstaEnElCatalogo()
    {
        var ct = TestContext.Current.CancellationToken;
        var resultado = await mcp.Cliente.CallToolAsync(
            "obtener_plantilla_semanal",
            new Dictionary<string, object?> { ["plantilla"] = "[SMOKE] Plantilla Que No Existe 629" },
            cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        var texto = resultado.Content.OfType<TextContentBlock>().Single().Text;

        texto.Should().NotStartWith("{", "el mensaje de plantilla inexistente es texto, no el objeto del camino feliz");
        texto.Should().Contain("[SMOKE] Plantilla Que No Existe 629");
    }
}
