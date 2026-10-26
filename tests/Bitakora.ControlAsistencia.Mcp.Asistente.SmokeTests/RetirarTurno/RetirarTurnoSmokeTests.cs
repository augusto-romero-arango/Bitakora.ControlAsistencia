using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.RetirarTurno;

// La invocacion real de retirar_turno con un turno que existe (MEF-ADR-0048 seccion 6, pieza 3)
// vive en CrearTurnoSmokeTests: retirar es tambien la limpieza del turno que aquel siembra, y
// separarlas dejaria un turno [TEST] huerfano en el catalogo de dev por cada corrida. Aqui quedan
// los caminos de retirar_turno que no necesitan sembrar nada.
public class RetirarTurnoSmokeTests(McpFixture mcp)
{
    // Error path que no toca el dominio: turno_id que no es GUID corta en el worker (mensaje .resx),
    // prueba que los recursos embebidos viajaron en el publish.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarTurno_RespondeElMensajeDeValidacion_CuandoElTurnoIdEstaEnBlanco()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "retirar_turno", new Dictionary<string, object?> { ["turno_id"] = "   " }, cancellationToken: ct);

        resultado.Content.OfType<TextContentBlock>().Single().Text
            .Should().StartWith("turno_id debe ser el id del turno");
    }

    // CA-3: turno_id inexistente -> TurnoNoExiste (404 del detalle real de Programacion, sin DELETE),
    // remite a buscar_turnos.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarTurno_RespondeTurnoNoExiste_CuandoElTurnoIdNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoInexistente = Guid.CreateVersion7().ToString();

        var resultado = await mcp.Cliente.CallToolAsync(
            "retirar_turno", new Dictionary<string, object?> { ["turno_id"] = turnoInexistente }, cancellationToken: ct);

        resultado.Content.OfType<TextContentBlock>().Single().Text
            .Should().Contain(turnoInexistente).And.Contain("buscar_turnos");
    }
}
