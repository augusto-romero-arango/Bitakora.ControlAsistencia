using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.CrearTurnoDeHorarioAbierto;

public class CrearTurnoDeHorarioAbiertoSmokeTests(McpFixture mcp, ProgramacionApiFixture programacion)
{
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearTurnoDeHorarioAbierto_CreaYRetiraElTurno_CuandoLaDuracionEsSeisHoras()
    {
        var ct = TestContext.Current.CancellationToken;
        var nombre = $"[TEST] Horario abierto MCP {Guid.CreateVersion7()}";

        var creado = await mcp.Cliente.CallToolAsync(
            "crear_turno_de_horario_abierto",
            new Dictionary<string, object?> { ["nombre"] = nombre, ["duracion"] = "6:00" },
            cancellationToken: ct);
        creado.IsError.Should().NotBeTrue();
        creado.Content.OfType<TextContentBlock>().Single().Text.Should().Contain(nombre);

        using var ficha = await programacion.Client.EsperarFichaAsync(nombre, ct);
        ficha.RootElement.GetProperty("nombre").GetString().Should().Be(nombre);

        var retirado = await mcp.Cliente.CallToolAsync(
            "retirar_turno", new Dictionary<string, object?> { ["turno"] = nombre }, cancellationToken: ct);
        retirado.IsError.Should().NotBeTrue();
    }
}
