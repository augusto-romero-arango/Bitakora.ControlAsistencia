using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.BuscarSedes;

public class BuscarSedesSmokeTests(McpFixture mcp)
{
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task BuscarSedes_DevuelveLaSedeRegistrada_CuandoSeBuscaPorNombre()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = Guid.CreateVersion7().ToString("N");
        var codigo = $"TEST-{sufijo}";
        var nombre = $"[TEST] Sede Buscar {sufijo}";

        var registrada = await mcp.Cliente.CallToolAsync(
            "registrar_sede",
            new Dictionary<string, object?>
            {
                ["codigo"] = codigo,
                ["nombre"] = nombre,
                ["ciudad"] = $"[TEST] Ciudad {sufijo}"
            },
            cancellationToken: ct);
        registrada.IsError.Should().NotBeTrue();

        var encontrada = await Polling.WaitUntilAsync(async () =>
        {
            var resultado = await mcp.Cliente.CallToolAsync(
                "buscar_sedes", new Dictionary<string, object?> { ["nombre"] = sufijo }, cancellationToken: ct);
            resultado.IsError.Should().NotBeTrue();
            var texto = resultado.Content.OfType<TextContentBlock>().Single().Text;
            if (!texto.StartsWith('{'))
                return null;

            var documento = JsonDocument.Parse(texto);
            return documento.RootElement.GetProperty("sedes").GetArrayLength() > 0 ? documento : null;
        }, TimeSpan.FromSeconds(60));

        using (encontrada)
        {
            var sede = encontrada.RootElement.GetProperty("sedes")[0];
            sede.GetProperty("codigo").GetString().Should().Be(codigo);
            sede.GetProperty("coincidePor").EnumerateArray().Select(m => m.GetString())
                .Should().Contain(m => m!.StartsWith("nombre:"));
        }
    }
}
