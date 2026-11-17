using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.ListarPlantillas;

public class ListarPlantillasSmokeTests(McpFixture mcp)
{
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarPlantillas_DevuelveElSobreSinTotalOLaFraseDeVacio_CuandoSeInvocaSinParametros()
    {
        var ct = TestContext.Current.CancellationToken;
        var resultado = await mcp.Cliente.CallToolAsync(
            "listar_plantillas", new Dictionary<string, object?>(), cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        var texto = resultado.Content.OfType<TextContentBlock>().Single().Text;

        if (!texto.TrimStart().StartsWith('{'))
        {
            texto.Should().NotBeNullOrWhiteSpace("sin plantillas la tool responde una frase");
            return;
        }

        using var json = JsonDocument.Parse(texto);
        var raiz = json.RootElement;
        raiz.TryGetProperty("total", out _).Should().BeFalse("el sobre del catalogo no trae total");

        var plantillas = raiz.GetProperty("plantillas").EnumerateArray().ToList();
        plantillas.Should().HaveCount(raiz.GetProperty("mostrando").GetInt32());

        foreach (var plantilla in plantillas)
        {
            plantilla.GetProperty("id").GetString().Should().NotBeNullOrWhiteSpace();
            plantilla.GetProperty("nombre").GetString().Should().NotBeNullOrWhiteSpace();
            plantilla.GetProperty("diasDelCiclo").GetInt32().Should().BeGreaterThan(0);

            if (plantilla.TryGetProperty("incompleta", out var incompleta))
                incompleta.GetBoolean().Should().BeTrue("incompleta solo viaja cuando es true");
            plantilla.TryGetProperty("conAdvertencias", out _).Should()
                .BeFalse("la plantilla ya no se audita contra su Jornada");
        }
    }
}
