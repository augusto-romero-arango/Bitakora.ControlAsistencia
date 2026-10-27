using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.CrearPlantillaRotativa;

public class CrearPlantillaRotativaSmokeTests(McpFixture mcp, ProgramacionApiFixture programacion)
{
    private static string TextoDe(CallToolResult resultado) =>
        resultado.Content.OfType<TextContentBlock>().Single().Text;

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearPlantillaRotativa_CreaCompletaConBloqueDeTurnoIdYDeFranjaYLaRetira_CuandoLosBloquesSumanTresDias()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = Guid.CreateVersion7();
        var turnoId = await CrearTurnoCompletoAsync($"[TEST] Turno Rotativa {sufijo}", ct);

        var bloques = $$"""[{"dias":2,"turno_id":"{{turnoId}}"},{"dias":1,"franja":"07:00-15:00"}]""";
        var creada = await mcp.Cliente.CallToolAsync(
            "crear_plantilla_rotativa",
            new Dictionary<string, object?> { ["nombre"] = $"[TEST] Rotativa {sufijo}", ["bloques"] = bloques },
            cancellationToken: ct);
        creada.IsError.Should().NotBeTrue();

        using var texto = JsonDocument.Parse(TextoDe(creada));
        var plantillaId = texto.RootElement.GetProperty("plantilla").GetProperty("id").GetString()!;
        texto.RootElement.GetProperty("plantilla").GetProperty("diasDelCiclo").GetInt32().Should().Be(3);
        texto.RootElement.GetProperty("diasAsignados").GetInt32().Should().Be(3);
        texto.RootElement.GetProperty("completa").GetBoolean().Should().BeTrue();
        texto.RootElement.GetProperty("bloques").GetArrayLength().Should().Be(2);
        texto.RootElement.TryGetProperty("diasRechazados", out _).Should().BeFalse();

        await RetirarAsync(plantillaId, turnoId, ct);
    }

    // jornada_id valido recorre la coercion de la extension MCP (MEF-ADR-0048 seccion 6). La
    // Jornada predeterminada solo se lee, nunca se cambia (CA-ADR-0040).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearPlantillaRotativa_NaceAsociadaALaJornada_CuandoSeIndicaJornadaId()
    {
        var ct = TestContext.Current.CancellationToken;
        var jornada = await SembradoDeJornadas.PredeterminadaActualAsync(mcp, ct);
        var jornadaId = jornada.GetProperty("jornada_id").GetString()!;
        var sufijo = Guid.CreateVersion7();
        var turnoId = await CrearTurnoCompletoAsync($"[TEST] Turno Rotativa Jornada {sufijo}", ct);

        var creada = await mcp.Cliente.CallToolAsync(
            "crear_plantilla_rotativa",
            new Dictionary<string, object?>
            {
                ["nombre"] = $"[TEST] Rotativa Jornada {sufijo}",
                ["bloques"] = $$"""[{"dias":1,"turno_id":"{{turnoId}}"}]""",
                ["jornada_id"] = jornadaId
            },
            cancellationToken: ct);
        creada.IsError.Should().NotBeTrue();

        using var texto = JsonDocument.Parse(TextoDe(creada));
        var plantillaId = texto.RootElement.GetProperty("plantilla").GetProperty("id").GetString()!;
        texto.RootElement.GetProperty("jornada").GetProperty("jornada_id").GetString().Should().Be(jornadaId);

        await RetirarAsync(plantillaId, turnoId, ct);
    }

    private async Task<string> CrearTurnoCompletoAsync(string nombre, CancellationToken ct)
    {
        (await mcp.Cliente.CallToolAsync(
            "crear_turno", new Dictionary<string, object?> { ["nombre"] = nombre }, cancellationToken: ct))
            .IsError.Should().NotBeTrue();
        using var ficha = await programacion.Client.EsperarFichaAsync(nombre, ct);
        var turnoId = ficha.RootElement.GetProperty("id").GetString()!;
        (await mcp.Cliente.CallToolAsync(
            "agregar_franja",
            new Dictionary<string, object?> { ["turno_id"] = turnoId, ["inicio"] = "06:00", ["fin"] = "14:00" },
            cancellationToken: ct)).IsError.Should().NotBeTrue();
        return turnoId;
    }

    private async Task RetirarAsync(string plantillaId, string turnoId, CancellationToken ct)
    {
        var retirada = await Polling.WaitUntilAsync(async () =>
        {
            var r = await mcp.Cliente.CallToolAsync(
                "retirar_plantilla",
                new Dictionary<string, object?> { ["plantilla_id"] = plantillaId },
                cancellationToken: ct);
            var contenido = TextoDe(r);
            return contenido.TrimStart().StartsWith('{') ? JsonDocument.Parse(contenido) : null;
        }, CatalogoDeTurnos.TimeoutPolling);
        retirada.Dispose();

        await mcp.Cliente.CallToolAsync(
            "retirar_turno", new Dictionary<string, object?> { ["turno_id"] = turnoId }, cancellationToken: ct);
    }
}
