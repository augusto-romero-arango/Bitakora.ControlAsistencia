using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.CrearTurno;

// El ciclo crear -> retirar va junto (CA-6): retirar_turno es tambien la limpieza del turno que
// este smoke siembra en el catalogo de dev. Los caminos de retirar_turno que no siembran nada
// viven en RetirarTurno/RetirarTurnoSmokeTests.cs.
public class CrearTurnoSmokeTests(McpFixture mcp, ProgramacionApiFixture programacion)
{
    // Recorre la cadena completa: host MCP -> worker -> HttpClient tipado -> Function App de
    // Programacion -> event store. El assert de creacion vive DENTRO del polling (materializacion
    // asincronica del catalogo); retirar_turno limpia el turno sembrado por este mismo test.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearTurno_CreaYRetiraElTurno_CuandoEsDescansoNoSeEnvia()
    {
        var ct = TestContext.Current.CancellationToken;
        var nombre = $"[TEST] Turno MCP {Guid.CreateVersion7()}";

        var creado = await mcp.Cliente.CallToolAsync(
            "crear_turno", new Dictionary<string, object?> { ["nombre"] = nombre }, cancellationToken: ct);
        creado.IsError.Should().NotBeTrue();
        creado.Content.OfType<TextContentBlock>().Single().Text.Should().Contain(nombre);

        using var ficha = await programacion.Client.EsperarFichaAsync(nombre, ct);
        ficha.RootElement.GetProperty("esDescanso").GetBoolean().Should().BeFalse();

        var retirado = await mcp.Cliente.CallToolAsync(
            "retirar_turno", new Dictionary<string, object?> { ["turno"] = nombre }, cancellationToken: ct);
        retirado.IsError.Should().NotBeTrue();
        retirado.Content.OfType<TextContentBlock>().Single().Text.Should().Contain(nombre);

        await Polling.WaitUntilAsync(
            async () =>
            {
                var candidato = await programacion.Client.BuscarFichaAsync(nombre, ct);
                candidato?.Dispose();
                return candidato is null ? new object() : null;
            },
            CatalogoDeTurnos.TimeoutPolling);
    }

    // Variante es_descanso=true (CA-6): la ficha debe llegar con esDescanso true; el descanso
    // nunca admite franjas, asi que no hay nada mas que verificar en el catalogo.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearTurno_CreaUnDescanso_CuandoEsDescansoEsTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        var nombre = $"[TEST] Descanso MCP {Guid.CreateVersion7()}";

        var creado = await mcp.Cliente.CallToolAsync(
            "crear_turno",
            new Dictionary<string, object?> { ["nombre"] = nombre, ["es_descanso"] = true },
            cancellationToken: ct);
        creado.IsError.Should().NotBeTrue();

        using var ficha = await programacion.Client.EsperarFichaAsync(nombre, ct);
        ficha.RootElement.GetProperty("esDescanso").GetBoolean().Should().BeTrue();

        await mcp.Cliente.CallToolAsync(
            "retirar_turno", new Dictionary<string, object?> { ["turno"] = nombre }, cancellationToken: ct);
    }

    // Error path que no toca el dominio: nombre en blanco corta en el worker (mensaje .resx),
    // prueba que los recursos embebidos viajaron en el publish.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearTurno_RespondeElMensajeDeValidacion_CuandoElNombreEstaEnBlanco()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "crear_turno", new Dictionary<string, object?> { ["nombre"] = "   " }, cancellationToken: ct);

        resultado.Content.OfType<TextContentBlock>().Single().Text
            .Should().Be("'nombre' es obligatorio.");
    }
}
