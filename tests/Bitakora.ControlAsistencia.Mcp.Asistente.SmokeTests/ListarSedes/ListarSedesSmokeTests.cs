using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.ListarSedes;

public class ListarSedesSmokeTests(McpFixture mcp)
{
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarSedes_DevuelveElSobreSinTotalOLaFraseDeVacio_CuandoSeInvocaSinParametros()
    {
        var ct = TestContext.Current.CancellationToken;
        var resultado = await mcp.Cliente.CallToolAsync(
            "listar_sedes", new Dictionary<string, object?>(), cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        var texto = resultado.Content.OfType<TextContentBlock>().Single().Text;

        if (!texto.TrimStart().StartsWith('{'))
        {
            texto.Should().NotBeNullOrWhiteSpace("sin sedes activas la tool responde una frase");
            return;
        }

        using var json = JsonDocument.Parse(texto);
        var raiz = json.RootElement;
        raiz.TryGetProperty("total", out _).Should().BeFalse("el sobre del catalogo no trae total");

        var sedes = raiz.GetProperty("sedes").EnumerateArray().ToList();
        sedes.Should().HaveCount(raiz.GetProperty("mostrando").GetInt32());
        foreach (var sede in sedes)
        {
            sede.GetProperty("codigo").GetString().Should().NotBeNullOrWhiteSpace();
            sede.GetProperty("nombre").GetString().Should().NotBeNullOrWhiteSpace();
        }
    }
}
