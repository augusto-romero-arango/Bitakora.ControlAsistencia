using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.ModificarLimitesJornada;

public class ModificarLimitesJornadaSmokeTests(McpFixture mcp)
{
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ModificarLimitesJornada_CambiaLaDescripcion_CuandoSeReemplazanLasHorasSemanales()
    {
        var ct = TestContext.Current.CancellationToken;
        var (creada, n) = await SembradoDeJornadas.CrearAsync(mcp, ct);
        var id = creada.GetProperty("jornada_id").GetString()!;

        var resultado = await mcp.Cliente.CallToolAsync(
            "modificar_limites_jornada",
            new Dictionary<string, object?>
            {
                ["jornada_id"] = id,
                ["horas_semanales"] = $"{n}:30",
                ["tope_diario"] = "1:00",
                ["minimo_diario"] = "0:00",
                ["dias_descanso_por_semana"] = 0
            },
            cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        using var json = JsonDocument.Parse(SembradoDeJornadas.Texto(resultado));
        json.RootElement.GetProperty("jornada_id").GetString().Should().Be(id);
        json.RootElement.GetProperty("descripcion").GetString()
            .Should().NotBe(creada.GetProperty("descripcion").GetString());
    }
}
