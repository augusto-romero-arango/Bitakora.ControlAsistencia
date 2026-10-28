using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.CrearPlantilla;

public class CrearPlantillaSmokeTests(McpFixture mcp, ProgramacionApiFixture programacion)
{
    private static string TextoDe(CallToolResult resultado) =>
        resultado.Content.OfType<TextContentBlock>().Single().Text;

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearPlantilla_CreaIncompletaConTurnoIdYFranjaYLaRetira_CuandoDiasDelCicloSuperaLasEntradas()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = Guid.CreateVersion7();
        var turnoId = await CrearTurnoCompletoAsync($"[TEST] Turno Crear Plantilla {sufijo}", ct);

        var dias = $$"""[{"dia":1,"turno_id":"{{turnoId}}"},{"dia":2,"franja":"07:00-15:00"}]""";
        var creada = await mcp.Cliente.CallToolAsync(
            "crear_plantilla",
            new Dictionary<string, object?> { ["nombre"] = $"[TEST] Ciclo {sufijo}", ["dias_del_ciclo"] = 3, ["dias"] = dias },
            cancellationToken: ct);
        creada.IsError.Should().NotBeTrue();

        using var texto = JsonDocument.Parse(TextoDe(creada));
        var plantillaId = texto.RootElement.GetProperty("plantilla").GetProperty("id").GetString()!;
        texto.RootElement.GetProperty("plantilla").GetProperty("diasDelCiclo").GetInt32().Should().Be(3);
        texto.RootElement.GetProperty("diasAsignados").GetInt32().Should().Be(2);
        texto.RootElement.GetProperty("completa").GetBoolean().Should().BeFalse();
        texto.RootElement.TryGetProperty("diasRechazados", out _).Should().BeFalse();
        texto.RootElement.GetProperty("turnosInline").EnumerateArray().Single()
            .GetProperty("nombre").GetString().Should().Be("07:00-15:00");

        await RetirarAsync(plantillaId, turnoId, ct);
    }

    // jornada_id valido recorre la coercion de la extension MCP (MEF-ADR-0048 seccion 6). La
    // Jornada predeterminada solo se lee, nunca se cambia (CA-ADR-0040).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearPlantilla_NaceAsociadaALaJornada_CuandoSeIndicaJornadaId()
    {
        var ct = TestContext.Current.CancellationToken;
        var jornada = await SembradoDeJornadas.PredeterminadaActualAsync(mcp, ct);
        var jornadaId = jornada.GetProperty("jornada_id").GetString()!;
        var sufijo = Guid.CreateVersion7();
        var turnoId = await CrearTurnoCompletoAsync($"[TEST] Turno Crear Plantilla Jornada {sufijo}", ct);

        var creada = await mcp.Cliente.CallToolAsync(
            "crear_plantilla",
            new Dictionary<string, object?>
            {
                ["nombre"] = $"[TEST] Ciclo Jornada {sufijo}",
                ["dias_del_ciclo"] = 1,
                ["dias"] = $$"""[{"dia":1,"turno_id":"{{turnoId}}"}]""",
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

    // retirar_plantilla lee la vista, que se materializa despues del POST: se reintenta hasta que
    // responde el JSON de exito.
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
