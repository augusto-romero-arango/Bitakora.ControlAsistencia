using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.DesmarcarDescansoAcumuladoDeJornada;

public class DesmarcarDescansoAcumuladoDeJornadaSmokeTests(McpFixture mcp)
{
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DesmarcarDescansoAcumuladoDeJornada_DevuelveLaJornadaSinLaMarca_CuandoLaJornadaEstabaMarcada()
    {
        var ct = TestContext.Current.CancellationToken;
        var (creada, _) = await SembradoDeJornadas.CrearAsync(mcp, ct, "Ordinaria", descansoAcumulado: true);
        var id = creada.GetProperty("jornada_id").GetString()!;

        var resultado = await mcp.Cliente.CallToolAsync(
            "desmarcar_descanso_acumulado_de_jornada",
            new Dictionary<string, object?> { ["jornada_id"] = id },
            cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        using var json = JsonDocument.Parse(SembradoDeJornadas.Texto(resultado));
        json.RootElement.GetProperty("jornada_id").GetString().Should().Be(id);
        json.RootElement.GetProperty("descanso_acumulado").GetBoolean().Should().BeFalse();
    }
}
