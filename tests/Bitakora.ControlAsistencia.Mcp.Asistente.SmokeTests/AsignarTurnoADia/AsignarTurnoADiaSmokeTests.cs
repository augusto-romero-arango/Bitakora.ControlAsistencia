using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.AsignarTurnoADia;

public class AsignarTurnoADiaSmokeTests(McpFixture mcp)
{
    private static string TextoDe(CallToolResult resultado) =>
        resultado.Content.OfType<TextContentBlock>().Single().Text;

    // CA-2/CA-4: plantilla inexistente -> PlantillaNoExiste con el id, sin PUT.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarTurnoADia_RespondePlantillaNoExiste_CuandoLaPlantillaNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var plantillaInexistente = Guid.CreateVersion7().ToString();

        var resultado = await mcp.Cliente.CallToolAsync(
            "asignar_turno_a_dia",
            new Dictionary<string, object?>
            {
                ["plantilla_id"] = plantillaInexistente,
                ["turno_id"] = Guid.CreateVersion7().ToString(),
                ["dia"] = "lunes"
            },
            cancellationToken: ct);

        TextoDe(resultado).Should().Contain(plantillaInexistente);
    }

    // Error path que no toca el dominio: un id que no es GUID corta en el worker (mensaje .resx), prueba
    // que los recursos embebidos de cada tool viajaron en el publish (mismo patron que AgregarFranja/
    // QuitarFranja). Ambas tools comparten 'plantilla_id' como primer chequeo -- un test por tool basta.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarTurnoADia_RespondeElMensajeDeValidacion_CuandoElPlantillaIdNoEsUnGuid()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "asignar_turno_a_dia",
            new Dictionary<string, object?> { ["plantilla_id"] = "no-es-guid", ["turno_id"] = Guid.CreateVersion7().ToString(), ["dia"] = "lunes" },
            cancellationToken: ct);

        TextoDe(resultado).Should().Contain("plantilla_id").And.Contain("no-es-guid");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarTurnoADia_RespondeElMensajeDeValidacion_CuandoElTurnoIdNoEsUnGuid()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "asignar_turno_a_dia",
            new Dictionary<string, object?> { ["plantilla_id"] = Guid.CreateVersion7().ToString(), ["turno_id"] = "no-es-guid", ["dia"] = "lunes" },
            cancellationToken: ct);

        TextoDe(resultado).Should().Contain("turno_id").And.Contain("no-es-guid");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarTurnoDeDia_RespondeElMensajeDeValidacion_CuandoElPlantillaIdNoEsUnGuid()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "quitar_turno_de_dia",
            new Dictionary<string, object?> { ["plantilla_id"] = "no-es-guid", ["dia"] = "lunes" },
            cancellationToken: ct);

        TextoDe(resultado).Should().Contain("plantilla_id").And.Contain("no-es-guid");
    }
}
