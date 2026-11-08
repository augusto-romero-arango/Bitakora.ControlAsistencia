using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.AsignarModalidadAJornada;

public class AsignarModalidadAJornadaSmokeTests(McpFixture mcp)
{
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarModalidadAJornada_DevuelveLaJornadaConModalidad_CuandoLaJornadaExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var (creada, _) = await SembradoDeJornadas.CrearAsync(mcp, ct);
        var id = creada.GetProperty("jornada_id").GetString()!;

        var resultado = await mcp.Cliente.CallToolAsync(
            "asignar_modalidad_a_jornada",
            new Dictionary<string, object?> { ["jornada_id"] = id, ["modalidad"] = "Ordinaria" },
            cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        using var json = JsonDocument.Parse(SembradoDeJornadas.Texto(resultado));
        json.RootElement.GetProperty("jornada_id").GetString().Should().Be(id);
        json.RootElement.GetProperty("modalidad").GetString().Should().Be("Ordinaria");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarModalidadAJornada_DevuelveAdvertenciaDelTopeDiario_CuandoOrdinariaExcedeElTope()
    {
        var ct = TestContext.Current.CancellationToken;
        var creada = await SembradoDeJornadas.CrearConTopeDiarioExcedidoAsync(mcp, ct);
        var id = creada.GetProperty("jornada_id").GetString()!;

        var resultado = await mcp.Cliente.CallToolAsync(
            "asignar_modalidad_a_jornada",
            new Dictionary<string, object?> { ["jornada_id"] = id, ["modalidad"] = "Ordinaria" },
            cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        using var json = JsonDocument.Parse(SembradoDeJornadas.Texto(resultado));
        json.RootElement.GetProperty("jornada_id").GetString().Should().Be(id);
        json.RootElement.GetProperty("modalidad").GetString().Should().Be("Ordinaria");
        json.RootElement.GetProperty("advertencias").EnumerateArray()
            .Select(a => a.GetString())
            .Should().NotBeEmpty().And.OnlyContain(a => !string.IsNullOrWhiteSpace(a));
    }
}
