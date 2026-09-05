using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.CrearPlantillaSemanal;

// El ciclo crear -> retirar va junto (CA-6 del issue #627, mismo precedente que
// CrearTurnoSmokeTests): retirar_plantilla_semanal es tambien la limpieza de la plantilla que
// este smoke siembra en el catalogo de dev.
public class CrearPlantillaSemanalSmokeTests(McpFixture mcp, ProgramacionApiFixture programacion)
{
    // Recorre la cadena completa: host MCP -> worker -> HttpClient tipado -> Function App de
    // Programacion -> event store, para el POST y para los PUT secuenciales. El assert de
    // creacion vive DENTRO del polling (materializacion asincronica del cuadro semanal);
    // retirar_plantilla_semanal limpia la plantilla sembrada por este mismo test.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearPlantillaSemanal_CreaYRetiraLaPlantilla_CuandoLosDosTurnosExisten()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = Guid.CreateVersion7();
        var turnoLunes = $"[TEST] Turno Lunes MCP {sufijo}";
        var turnoMartes = $"[TEST] Turno Martes MCP {sufijo}";
        var nombrePlantilla = $"[TEST] Plantilla MCP {sufijo}";

        foreach (var turno in new[] { turnoLunes, turnoMartes })
        {
            var creado = await mcp.Cliente.CallToolAsync(
                "crear_turno", new Dictionary<string, object?> { ["nombre"] = turno }, cancellationToken: ct);
            creado.IsError.Should().NotBeTrue();

            using var ficha = await programacion.Client.EsperarFichaAsync(turno, ct);

            var conFranja = await mcp.Cliente.CallToolAsync(
                "agregar_franja",
                new Dictionary<string, object?> { ["turno"] = turno, ["inicio"] = "06:00", ["fin"] = "14:00" },
                cancellationToken: ct);
            conFranja.IsError.Should().NotBeTrue();
        }

        var dias = $$"""
            [{"semana":1,"dia":"lunes","turno":"{{turnoLunes}}"},{"semana":1,"dia":"martes","turno":"{{turnoMartes}}"}]
            """;

        var creada = await mcp.Cliente.CallToolAsync(
            "crear_plantilla_semanal",
            new Dictionary<string, object?> { ["nombre"] = nombrePlantilla, ["dias"] = dias },
            cancellationToken: ct);
        creada.IsError.Should().NotBeTrue();

        using var textoCreada = JsonDocument.Parse(creada.Content.OfType<TextContentBlock>().Single().Text);
        var plantillaId = textoCreada.RootElement.GetProperty("plantilla").GetProperty("id").GetString()!;

        using (var cuadro = await programacion.EsperarCuadroAsync(plantillaId, ct))
            cuadro.RootElement.GetProperty("dias").GetArrayLength().Should().Be(2);

        var retirada = await mcp.Cliente.CallToolAsync(
            "retirar_plantilla_semanal",
            new Dictionary<string, object?> { ["plantilla"] = nombrePlantilla },
            cancellationToken: ct);
        retirada.IsError.Should().NotBeTrue();

        await Polling.WaitUntilAsync(
            async () =>
            {
                var candidato = await programacion.BuscarCuadroAsync(plantillaId, ct);
                candidato?.Dispose();
                return candidato is null ? new object() : null;
            },
            CatalogoDeTurnos.TimeoutPolling);

        foreach (var turno in new[] { turnoLunes, turnoMartes })
            await mcp.Cliente.CallToolAsync(
                "retirar_turno", new Dictionary<string, object?> { ["turno"] = turno }, cancellationToken: ct);
    }

    // CA-2: un turno inexistente en el catalogo no crea nada -- el mensaje TurnosNoExisten llega
    // desde el worker real, con el catalogo real de dev.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearPlantillaSemanal_RespondeTurnosNoExisten_CuandoElTurnoNoExisteEnElCatalogo()
    {
        var ct = TestContext.Current.CancellationToken;
        var nombrePlantilla = $"[TEST] Plantilla MCP {Guid.CreateVersion7()}";
        var turnoInexistente = $"[TEST] Turno inexistente {Guid.CreateVersion7()}";
        var dias = $$"""[{"semana":1,"dia":"lunes","turno":"{{turnoInexistente}}"}]""";

        var resultado = await mcp.Cliente.CallToolAsync(
            "crear_plantilla_semanal",
            new Dictionary<string, object?> { ["nombre"] = nombrePlantilla, ["dias"] = dias },
            cancellationToken: ct);

        resultado.Content.OfType<TextContentBlock>().Single().Text.Should().Contain(turnoInexistente);
    }
}
