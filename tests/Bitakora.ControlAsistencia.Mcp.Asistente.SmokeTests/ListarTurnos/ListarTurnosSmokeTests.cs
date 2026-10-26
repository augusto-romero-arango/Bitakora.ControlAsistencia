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
        var codigoSede = await Sembrado.RegistrarSedeAsync(mcp.Cliente, ct);
        var nombreTurno = $"[TEST] ListarTurnos Sede MCP {Guid.CreateVersion7()}";

        var creado = await mcp.Cliente.CallToolAsync(
            "crear_turno", new Dictionary<string, object?> { ["nombre"] = nombreTurno }, cancellationToken: ct);
        creado.IsError.Should().NotBeTrue();

        using var ficha = await programacion.Client.EsperarFichaAsync(nombreTurno, ct);
        var turnoId = ficha.RootElement.GetProperty("id").GetString()!;

        var agregada = await mcp.Cliente.CallToolAsync(
            "agregar_franja",
            new Dictionary<string, object?> { ["turno_id"] = turnoId, ["inicio"] = "14:00", ["fin"] = "22:00" },
            cancellationToken: ct);
        agregada.IsError.Should().NotBeTrue();

        var argumentosSede = new Dictionary<string, object?>
        {
            ["turno_id"] = turnoId,
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

        var sedesPorTurno = new Dictionary<string, IReadOnlyList<string>>();
        foreach (var turno in listado.RootElement.GetProperty("turnos").EnumerateArray())
        {
            var id = turno.GetProperty("id").GetString()!;
            sedesPorTurno[id] = await SedesDeLasFranjasAsync(id, ct);
        }

        var sedeId = sedesPorTurno[turnoId].Should().ContainSingle(
            "el turno sembrado tiene una sola franja, asignada a la sede del arrange").Subject;
        sedesPorTurno.Should().OnlyContain(
            t => t.Value.Contains(sedeId),
            $"todo turno devuelto con sede={codigoSede} tiene alguna franja en esa sede");

        await mcp.Cliente.CallToolAsync(
            "retirar_turno", new Dictionary<string, object?> { ["turno"] = nombreTurno }, cancellationToken: ct);
    }

    private async Task<IReadOnlyList<string>> SedesDeLasFranjasAsync(string turnoId, CancellationToken ct)
    {
        var detalle = await programacion.Client.GetStringAsync($"/api/programacion/turnos/{turnoId}", ct);
        using var documento = JsonDocument.Parse(detalle);
        return
        [
            .. documento.RootElement.GetProperty("franjas").EnumerateArray()
                .Where(f => f.TryGetProperty("sedeId", out var sede) && sede.ValueKind == JsonValueKind.String)
                .Select(f => f.GetProperty("sedeId").GetString()!)
        ];
    }
}
