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

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearJornada_DevuelveLaModalidad_CuandoSeDeclaraAlCrear()
    {
        var ct = TestContext.Current.CancellationToken;
        System.Text.Json.JsonElement jornada = default;
        var creada = false;

        for (var intento = 0; intento < 10 && !creada; intento++)
        {
            var args = LimitesDeSmoke.Sortear().ComoArgumentos();
            args["modalidad"] = "Ordinaria";
            var resultado = await mcp.Cliente.CallToolAsync("crear_jornada", args, cancellationToken: ct);
            try
            {
                using var json = System.Text.Json.JsonDocument.Parse(SembradoDeJornadas.Texto(resultado));
                jornada = json.RootElement.Clone();
                creada = jornada.TryGetProperty("jornada_id", out _);
            }
            catch (System.Text.Json.JsonException)
            {
            }
        }

        creada.Should().BeTrue("algun sorteo de limites no deberia repetirse");
        jornada.GetProperty("modalidad").GetString().Should().Be("Ordinaria");
    }
}
