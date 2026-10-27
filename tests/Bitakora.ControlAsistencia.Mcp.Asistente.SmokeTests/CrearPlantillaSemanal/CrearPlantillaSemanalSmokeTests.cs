using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.CrearPlantillaSemanal;

public class CrearPlantillaSemanalSmokeTests(McpFixture mcp, ProgramacionApiFixture programacion)
{
    private static string TextoDe(CallToolResult resultado) =>
        resultado.Content.OfType<TextContentBlock>().Single().Text;

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearPlantillaSemanal_CreaIncompletaConTurnoIdYFranjaYLaRetira_CuandoSoloSeListanDosDias()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = Guid.CreateVersion7();
        var turnoId = await CrearTurnoCompletoAsync($"[TEST] Turno Semanal {sufijo}", ct);

        var dias = $$"""[{"dia":"lunes","turno_id":"{{turnoId}}"},{"dia":"martes","franja":"07:00-15:00"}]""";
        var creada = await mcp.Cliente.CallToolAsync(
            "crear_plantilla_semanal",
            new Dictionary<string, object?> { ["nombre"] = $"[TEST] Semanal {sufijo}", ["dias"] = dias },
            cancellationToken: ct);
        creada.IsError.Should().NotBeTrue();

        using var texto = JsonDocument.Parse(TextoDe(creada));
        var plantillaId = texto.RootElement.GetProperty("plantilla").GetProperty("id").GetString()!;
        texto.RootElement.GetProperty("plantilla").GetProperty("diasDelCiclo").GetInt32().Should().Be(7);
        texto.RootElement.GetProperty("diasAsignados").GetInt32().Should().Be(2);
        texto.RootElement.GetProperty("completa").GetBoolean().Should().BeFalse();
        texto.RootElement.TryGetProperty("diasRechazados", out _).Should().BeFalse();
        texto.RootElement.GetProperty("turnosInline").EnumerateArray().Single()
            .GetProperty("nombre").GetString().Should().Be("07:00-15:00");

        await RetirarAsync(plantillaId, turnoId, ct);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearPlantillaSemanal_RespondeTurnosNoExisten_CuandoElTurnoNoExisteEnElCatalogo()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoInexistente = Guid.CreateVersion7().ToString();
        var dias = $$"""[{"dia":"lunes","turno_id":"{{turnoInexistente}}"}]""";

        var resultado = await mcp.Cliente.CallToolAsync(
            "crear_plantilla_semanal",
            new Dictionary<string, object?> { ["nombre"] = $"[TEST] Semanal {Guid.CreateVersion7()}", ["dias"] = dias },
            cancellationToken: ct);

        TextoDe(resultado).Should().Contain(turnoInexistente);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearPlantillaSemanal_RemiteACrearPlantilla_CuandoUnaEntradaTraeSemanaDistintaDeUno()
    {
        var ct = TestContext.Current.CancellationToken;
        const string dias = """[{"semana":2,"dia":"lunes","franja":"03:17-11:43"}]""";

        var resultado = await mcp.Cliente.CallToolAsync(
            "crear_plantilla_semanal",
            new Dictionary<string, object?> { ["nombre"] = $"[TEST] Semanal {Guid.CreateVersion7()}", ["dias"] = dias },
            cancellationToken: ct);

        TextoDe(resultado).Should().Contain("crear_plantilla");
    }

    // La Jornada predeterminada actual solo se lee, nunca se cambia (CA-ADR-0040).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearPlantillaSemanal_NaceAsociadaALaJornada_CuandoSeIndicaJornadaId()
    {
        var ct = TestContext.Current.CancellationToken;
        var jornada = await SembradoDeJornadas.PredeterminadaActualAsync(mcp, ct);
        var jornadaId = jornada.GetProperty("jornada_id").GetString()!;
        var sufijo = Guid.CreateVersion7();
        var turnoId = await CrearTurnoCompletoAsync($"[TEST] Turno Semanal Jornada {sufijo}", ct);

        var creada = await mcp.Cliente.CallToolAsync(
            "crear_plantilla_semanal",
            new Dictionary<string, object?>
            {
                ["nombre"] = $"[TEST] Semanal Jornada {sufijo}",
                ["dias"] = $$"""[{"dia":"lunes","turno_id":"{{turnoId}}"}]""",
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
