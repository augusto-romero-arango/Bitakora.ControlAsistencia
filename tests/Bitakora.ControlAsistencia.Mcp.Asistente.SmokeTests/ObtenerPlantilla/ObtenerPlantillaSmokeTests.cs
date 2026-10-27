using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.ObtenerPlantilla;

public class ObtenerPlantillaSmokeTests(McpFixture mcp, ProgramacionApiFixture programacion)
{
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerPlantilla_DevuelveElCuadroDelCiclo_CuandoLaPlantillaExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var nombre = $"[TEST] Plantilla Obtener {Guid.CreateVersion7():N}";
        var plantillaId = await programacion.Client.CrearPlantillaAsync(nombre, 3, ct);

        var cuadro = await Polling.WaitUntilAsync(async () =>
        {
            var resultado = await mcp.Cliente.CallToolAsync(
                "obtener_plantilla",
                new Dictionary<string, object?> { ["plantilla_id"] = plantillaId.ToString() },
                cancellationToken: ct);
            resultado.IsError.Should().NotBeTrue();
            var texto = resultado.Content.OfType<TextContentBlock>().Single().Text;
            return texto.TrimStart().StartsWith('{') ? JsonDocument.Parse(texto) : null;
        }, CatalogoDeTurnos.TimeoutPolling);

        using (cuadro)
        {
            var raiz = cuadro.RootElement;
            raiz.GetProperty("id").GetString().Should().Be(plantillaId.ToString());
            raiz.GetProperty("nombre").GetString().Should().Be(nombre);
            raiz.GetProperty("diasDelCiclo").GetInt32().Should().Be(3);
            raiz.GetProperty("dias").EnumerateArray().Select(d => d.GetProperty("dia").GetInt32())
                .Should().Equal(1, 2, 3);
        }
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerPlantilla_RespondeMensajeNoExiste_CuandoElIdNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var plantillaInexistente = Guid.CreateVersion7().ToString();
        var resultado = await mcp.Cliente.CallToolAsync(
            "obtener_plantilla",
            new Dictionary<string, object?> { ["plantilla_id"] = plantillaInexistente },
            cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        var texto = resultado.Content.OfType<TextContentBlock>().Single().Text;

        texto.Should().NotStartWith("{");
        texto.Should().Contain(plantillaInexistente);
    }
}
