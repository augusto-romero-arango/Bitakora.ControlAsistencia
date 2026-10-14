using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.AsignarJornadaPredeterminada;

public class AsignarJornadaPredeterminadaSmokeTests(McpFixture mcp)
{
    // Reasignar la predeterminada actual es un no-op en el servidor: no cambia la de dev.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarJornadaPredeterminada_ConfirmaLaPredeterminada_CuandoSeReasignaLaActual()
    {
        var ct = TestContext.Current.CancellationToken;
        var actual = await SembradoDeJornadas.PredeterminadaActualAsync(mcp, ct);
        var id = actual.GetProperty("jornada_id").GetString()!;

        var resultado = await mcp.Cliente.CallToolAsync(
            "asignar_jornada_predeterminada",
            new Dictionary<string, object?> { ["jornada_id"] = id },
            cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        using var json = JsonDocument.Parse(SembradoDeJornadas.Texto(resultado));
        json.RootElement.GetProperty("jornada_id").GetString().Should().Be(id);
        json.RootElement.GetProperty("es_predeterminada").GetBoolean().Should().BeTrue();
    }
}
