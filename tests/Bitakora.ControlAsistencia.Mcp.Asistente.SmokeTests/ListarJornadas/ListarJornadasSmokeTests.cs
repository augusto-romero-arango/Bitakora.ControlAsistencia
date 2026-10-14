using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.ListarJornadas;

public class ListarJornadasSmokeTests(McpFixture mcp)
{
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarJornadas_DevuelvePaginaConForma_CuandoSeInvocaSinParametros()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "listar_jornadas", new Dictionary<string, object?>(), cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        using var json = JsonDocument.Parse(SembradoDeJornadas.Texto(resultado));
        var jornadas = json.RootElement.GetProperty("jornadas").EnumerateArray().ToList();

        jornadas.Should().NotBeEmpty();
        json.RootElement.GetProperty("mostrando").GetInt32().Should().Be(jornadas.Count);
        jornadas.Should().OnlyContain(j =>
            !string.IsNullOrWhiteSpace(j.GetProperty("jornada_id").GetString())
            && !string.IsNullOrWhiteSpace(j.GetProperty("descripcion").GetString()));
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarJornadas_ContieneUnaJornadaPredeterminada_CuandoSeRecorreTodoElCatalogo()
    {
        var ct = TestContext.Current.CancellationToken;

        var predeterminada = await SembradoDeJornadas.PredeterminadaActualAsync(mcp, ct);

        predeterminada.GetProperty("es_predeterminada").GetBoolean().Should().BeTrue();
    }
}
