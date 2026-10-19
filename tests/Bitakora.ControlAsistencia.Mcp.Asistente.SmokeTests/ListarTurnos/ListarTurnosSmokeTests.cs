using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.ListarTurnos;

public class ListarTurnosSmokeTests(McpFixture mcp, ProgramacionApiFixture programacion)
{
    private static string TextoDe(CallToolResult resultado) =>
        resultado.Content.OfType<TextContentBlock>().Single().Text;

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnos_DevuelveElSobreSinTotalConDatosReales_CuandoSeInvocaSinParametros()
    {
        var ct = TestContext.Current.CancellationToken;
        var resultado = await mcp.Cliente.CallToolAsync(
            "listar_turnos", new Dictionary<string, object?>(), cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        using var json = JsonDocument.Parse(TextoDe(resultado));
        var raiz = json.RootElement;

        raiz.TryGetProperty("total", out _).Should().BeFalse("el sobre del catalogo no trae total");
        var turnos = raiz.GetProperty("turnos").EnumerateArray().ToList();
        turnos.Should().HaveCount(raiz.GetProperty("mostrando").GetInt32());

        foreach (var turno in turnos)
        {
            turno.GetProperty("id").GetString().Should().NotBeNullOrWhiteSpace();
            turno.GetProperty("nombre").GetString().Should().NotBeNullOrWhiteSpace();
            turno.GetProperty("horario").GetString().Should().NotBeNullOrWhiteSpace();

            foreach (var senal in new[] { "enConstruccion", "esDescanso" })
                if (turno.TryGetProperty(senal, out var valor))
                    valor.GetBoolean().Should().BeTrue($"{senal} solo viaja cuando es true");
        }
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnos_DevuelveSoloLosTurnosDeLaSede_CuandoSeFiltraPorSede()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = Guid.CreateVersion7();
        var codigoSede = $"TEST-{sufijo}";
        var nombreTurno = $"[TEST] ListarTurnos Sede MCP {sufijo}";

        await mcp.Cliente.CallToolAsync(
            "registrar_sede",
            new Dictionary<string, object?> { ["codigo"] = codigoSede, ["nombre"] = "[TEST] Sede ListarTurnos" },
            cancellationToken: ct);

        var creado = await mcp.Cliente.CallToolAsync(
            "crear_turno", new Dictionary<string, object?> { ["nombre"] = nombreTurno }, cancellationToken: ct);
        creado.IsError.Should().NotBeTrue();

        using var ficha = await programacion.Client.EsperarFichaAsync(nombreTurno, ct);
        var turnoId = ficha.RootElement.GetProperty("id").GetString()!;

        var agregada = await mcp.Cliente.CallToolAsync(
            "agregar_franja",
            new Dictionary<string, object?> { ["turno"] = nombreTurno, ["inicio"] = "14:00", ["fin"] = "22:00" },
            cancellationToken: ct);
        agregada.IsError.Should().NotBeTrue();

        var argumentosSede = new Dictionary<string, object?>
        {
            ["turno"] = nombreTurno,
            ["franja"] = "14:00",
            ["codigo_sede"] = codigoSede
        };

        // La sede recien registrada y la franja recien agregada se materializan de forma asincrona;
        // Polling reintenta la asignacion hasta que responde el eco JSON.
        using var asignada = await Polling.WaitUntilAsync(
            async () =>
            {
                var r = await mcp.Cliente.CallToolAsync(
                    "asignar_sede_franja", argumentosSede, cancellationToken: ct);
                var texto = TextoDe(r);
                return texto.TrimStart().StartsWith('{') ? JsonDocument.Parse(texto) : null;
            },
            CatalogoDeTurnos.TimeoutPolling);

        using var listado = await Polling.WaitUntilAsync(
            async () =>
            {
                var r = await mcp.Cliente.CallToolAsync(
                    "listar_turnos", new Dictionary<string, object?> { ["sede"] = codigoSede },
                    cancellationToken: ct);
                var texto = TextoDe(r);
                if (!texto.TrimStart().StartsWith('{'))
                    return null;

                var documento = JsonDocument.Parse(texto);
                var aparece = documento.RootElement.GetProperty("turnos").EnumerateArray()
                    .Any(t => t.GetProperty("id").GetString() == turnoId);
                return aparece ? documento : null;
            },
            CatalogoDeTurnos.TimeoutPolling);

        foreach (var turno in listado.RootElement.GetProperty("turnos").EnumerateArray())
        {
            var id = turno.GetProperty("id").GetString();
            var detalle = await programacion.Client.GetStringAsync($"/api/programacion/turnos/{id}", ct);
            using var documento = JsonDocument.Parse(detalle);
            documento.RootElement.GetProperty("franjas").EnumerateArray()
                .Any(f => f.TryGetProperty("sedeId", out var sede) && sede.ValueKind == JsonValueKind.String)
                .Should().BeTrue($"el turno {id} devuelto con sede={codigoSede} debe tener una franja con sede");
        }

        await mcp.Cliente.CallToolAsync(
            "retirar_turno", new Dictionary<string, object?> { ["turno"] = nombreTurno }, cancellationToken: ct);
    }
}
