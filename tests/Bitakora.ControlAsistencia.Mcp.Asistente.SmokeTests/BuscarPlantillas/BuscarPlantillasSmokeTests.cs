using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.BuscarPlantillas;

public class BuscarPlantillasSmokeTests(McpFixture mcp, ProgramacionApiFixture programacion)
{
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task BuscarPlantillas_DevuelveLaPlantillaConMotivoDeNombre_CuandoSeBuscaPorNombre()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = Guid.CreateVersion7().ToString("N");
        var nombre = $"[TEST] Plantilla Buscar {sufijo}";
        var plantillaId = await programacion.Client.CrearPlantillaAsync(nombre, 3, ct);

        var encontrada = await Polling.WaitUntilAsync(async () =>
        {
            var resultado = await mcp.Cliente.CallToolAsync(
                "buscar_plantillas", new Dictionary<string, object?> { ["nombre"] = sufijo }, cancellationToken: ct);
            resultado.IsError.Should().NotBeTrue();
            var texto = resultado.Content.OfType<TextContentBlock>().Single().Text;
            if (!texto.TrimStart().StartsWith('{'))
                return null;

            var documento = JsonDocument.Parse(texto);
            return documento.RootElement.GetProperty("plantillas").GetArrayLength() > 0 ? documento : null;
        }, CatalogoDeTurnos.TimeoutPolling);

        using (encontrada)
        {
            var candidata = encontrada.RootElement.GetProperty("plantillas").EnumerateArray()
                .Single(p => p.GetProperty("id").GetString() == plantillaId.ToString());
            candidata.GetProperty("nombre").GetString().Should().Be(nombre);
            candidata.GetProperty("diasDelCiclo").GetInt32().Should().Be(3);
            candidata.GetProperty("coincidePor").EnumerateArray().Should().NotBeEmpty();
        }
    }
}
