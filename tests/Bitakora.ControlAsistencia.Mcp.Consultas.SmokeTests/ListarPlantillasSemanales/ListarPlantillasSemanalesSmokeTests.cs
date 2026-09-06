using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.SmokeTests.ListarPlantillasSemanales;

public class ListarPlantillasSemanalesSmokeTests(McpFixture mcp)
{
    // CA-5 (issue #629): tool call real sin arrange -- dev puede tener 0 plantillas semanales
    // cargadas todavia, asi que se afirma la forma compacta y su consistencia interna
    // (mostrando == plantillas.Count), no valores puntuales.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarPlantillasSemanales_DevuelveLaFormaCompacta_CuandoSeInvocaSinFiltro()
    {
        var ct = TestContext.Current.CancellationToken;
        var resultado = await mcp.Cliente.CallToolAsync(
            "listar_plantillas_semanales", new Dictionary<string, object?>(), cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        var texto = resultado.Content.OfType<TextContentBlock>().Single().Text;

        using var json = JsonDocument.Parse(texto);
        var raiz = json.RootElement;

        var total = raiz.GetProperty("total").GetInt32();
        var plantillas = raiz.GetProperty("plantillas").EnumerateArray().ToList();

        total.Should().BeGreaterThanOrEqualTo(0);
        plantillas.Should().HaveCount(raiz.GetProperty("mostrando").GetInt32());

        foreach (var plantilla in plantillas)
        {
            plantilla.GetProperty("id").GetString().Should().NotBeNullOrWhiteSpace();
            plantilla.GetProperty("nombre").GetString().Should().NotBeNullOrWhiteSpace();
            plantilla.GetProperty("semanas").GetInt32().Should().BeGreaterThan(0);

            if (plantilla.TryGetProperty("incompleta", out var incompleta))
                incompleta.GetBoolean().Should().BeTrue("incompleta solo viaja cuando la plantilla no esta completa");
        }
    }
}
