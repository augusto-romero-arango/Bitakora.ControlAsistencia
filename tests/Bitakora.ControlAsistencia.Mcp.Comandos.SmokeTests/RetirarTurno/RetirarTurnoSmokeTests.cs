using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.RetirarTurno;

// La invocacion real de retirar_turno con un turno que existe (MEF-ADR-0048 seccion 6, pieza 3)
// vive en CrearTurnoSmokeTests: retirar es tambien la limpieza del turno que aquel siembra, y
// separarlas dejaria un turno [TEST] huerfano en el catalogo de dev por cada corrida. Aqui quedan
// los caminos de retirar_turno que no necesitan sembrar nada.
public class RetirarTurnoSmokeTests(McpFixture mcp)
{
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
