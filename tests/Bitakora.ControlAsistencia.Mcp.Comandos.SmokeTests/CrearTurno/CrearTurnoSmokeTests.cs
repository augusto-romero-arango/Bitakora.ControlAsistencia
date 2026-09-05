using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.CrearTurno;

// Combina crear_turno y retirar_turno (CA-6): retirar_turno es tambien la limpieza de este smoke,
// asi que no hace falta un proyecto de smoke separado por tool (issue #608, impacto esperado).
public class CrearTurnoSmokeTests(McpFixture mcp, ProgramacionApiFixture programacion)
{
    private static readonly TimeSpan TimeoutPolling = TimeSpan.FromSeconds(30);

    private async Task<JsonDocument?> BuscarFichaAsync(string nombre, CancellationToken ct)
    {
        var texto = await programacion.Client.GetStringAsync("/api/programacion/turnos", ct);
        using var documento = JsonDocument.Parse(texto);
        foreach (var turno in documento.RootElement.EnumerateArray())
            if (turno.GetProperty("nombre").GetString() == nombre)
                return JsonDocument.Parse(turno.GetRawText());

        return null;
    }

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

        using var ficha = await Polling.WaitUntilAsync(() => BuscarFichaAsync(nombre, ct), TimeoutPolling);
        ficha.RootElement.GetProperty("esDescanso").GetBoolean().Should().BeFalse();

        var retirado = await mcp.Cliente.CallToolAsync(
            "retirar_turno", new Dictionary<string, object?> { ["turno"] = nombre }, cancellationToken: ct);
        retirado.IsError.Should().NotBeTrue();
        retirado.Content.OfType<TextContentBlock>().Single().Text.Should().Contain(nombre);

        await Polling.WaitUntilAsync(
            async () =>
            {
                var candidato = await BuscarFichaAsync(nombre, ct);
                candidato?.Dispose();
                return candidato is null ? new object() : null;
            },
            TimeoutPolling);
    }

    // Variante es_descanso=true (CA-6): la ficha debe llegar con esDescanso true; el descanso
    // nunca admite franjas, asi que no hay nada mas que verificar en el catalogo.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearTurno_CreaUnDescanso_CuandoEsDescansoEsTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        var nombre = $"[TEST] Descanso MCP {Guid.CreateVersion7()}";

        await mcp.Cliente.CallToolAsync(
            "crear_turno",
            new Dictionary<string, object?> { ["nombre"] = nombre, ["es_descanso"] = true },
            cancellationToken: ct);

        using var ficha = await Polling.WaitUntilAsync(() => BuscarFichaAsync(nombre, ct), TimeoutPolling);
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

    // Error path que no toca el dominio: turno en blanco corta en el worker (mensaje .resx),
    // prueba que los recursos embebidos viajaron en el publish.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarTurno_RespondeElMensajeDeValidacion_CuandoElTurnoEstaEnBlanco()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "retirar_turno", new Dictionary<string, object?> { ["turno"] = "   " }, cancellationToken: ct);

        resultado.Content.OfType<TextContentBlock>().Single().Text
            .Should().Be("'turno' es obligatorio.");
    }

    // CA-3: nombre inexistente -> TurnoNoExiste, resuelto contra el catalogo real de Programacion
    // (sin DELETE). El guid en el nombre garantiza que no colisiona con ningun turno sembrado.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarTurno_RespondeTurnoNoExiste_CuandoElNombreNoEstaEnElCatalogo()
    {
        var ct = TestContext.Current.CancellationToken;
        var nombre = $"[TEST] Turno que no existe {Guid.CreateVersion7()}";

        var resultado = await mcp.Cliente.CallToolAsync(
            "retirar_turno", new Dictionary<string, object?> { ["turno"] = nombre }, cancellationToken: ct);

        resultado.Content.OfType<TextContentBlock>().Single().Text
            .Should().StartWith($"No existe un turno con el nombre '{nombre}'.");
    }
}
