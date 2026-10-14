using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.CrearJornada;

public class CrearJornadaSmokeTests(McpFixture mcp)
{
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearJornada_DevuelveElIdYLaDescripcionDelServidor_CuandoLosLimitesSonValidos()
    {
        var ct = TestContext.Current.CancellationToken;

        var (jornada, _) = await SembradoDeJornadas.CrearAsync(mcp, ct);

        Guid.TryParse(jornada.GetProperty("jornada_id").GetString(), out _).Should().BeTrue();
        jornada.GetProperty("descripcion").GetString().Should().NotBeNullOrWhiteSpace();
        jornada.GetProperty("es_predeterminada").GetBoolean().Should().BeFalse();
    }
}
