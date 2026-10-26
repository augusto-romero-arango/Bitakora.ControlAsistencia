using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.ObtenerTurno;

public class ObtenerTurnoSmokeTests(McpFixture mcp)
{
    // CA-5 (issue #612): tool call real ampliada -- toma el primer id de listar_turnos y confirma
    // que completo viaja como bool y que ninguna franja usa la vieja notacion "(+1)" (CA-3). Los
    // datos son los reales de dev, asi que se afirma la forma y la consistencia, no valores puntuales.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerTurno_DevuelveCompletoYNotacionUnificada_CuandoSeConsultaElPrimerIdDelListado()
    {
        var ct = TestContext.Current.CancellationToken;

        var listado = await mcp.Cliente.CallToolAsync(
            "listar_turnos", new Dictionary<string, object?>(), cancellationToken: ct);
        var textoListado = listado.Content.OfType<TextContentBlock>().Single().Text;
        using var jsonListado = JsonDocument.Parse(textoListado);

        var turnos = jsonListado.RootElement.GetProperty("turnos").EnumerateArray().ToList();
        turnos.Should().NotBeEmpty("dev tiene turnos reales cargados");

        var primerId = turnos[0].GetProperty("id").GetString();

        foreach (var turno in turnos)
            turno.GetProperty("horario").GetString().Should().NotContain("(+");

        var resultado = await mcp.Cliente.CallToolAsync(
            "obtener_turno", new Dictionary<string, object?> { ["id"] = primerId }, cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        var texto = resultado.Content.OfType<TextContentBlock>().Single().Text;

        using var json = JsonDocument.Parse(texto);
        var raiz = json.RootElement;

        raiz.GetProperty("completo").ValueKind.Should().BeOneOf(JsonValueKind.True, JsonValueKind.False);

        raiz.GetProperty("horario").GetString().Should().NotContain("(+");

        foreach (var franja in raiz.GetProperty("franjas").EnumerateArray())
            franja.GetString().Should().NotContain("(+");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerTurno_MuestraHorarioAbierto_CuandoElTurnoSeCreoConCrearTurnoDeHorarioAbierto()
    {
        var ct = TestContext.Current.CancellationToken;
        var nombre = $"[TEST] Horario abierto catalogo {Guid.CreateVersion7()}";

        var creado = await mcp.Cliente.CallToolAsync(
            "crear_turno_de_horario_abierto",
            new Dictionary<string, object?> { ["nombre"] = nombre, ["duracion"] = "2:00" },
            cancellationToken: ct);
        creado.IsError.Should().NotBeTrue();
        using var eco = JsonDocument.Parse(creado.Content.OfType<TextContentBlock>().Single().Text);
        var turnoId = eco.RootElement.GetProperty("turno").GetProperty("id").GetString()!;

        var texto = await Polling.WaitUntilAsync(
            async () =>
            {
                var obtenido = await mcp.Cliente.CallToolAsync(
                    "obtener_turno", new Dictionary<string, object?> { ["id"] = turnoId }, cancellationToken: ct);
                var contenido = obtenido.Content.OfType<TextContentBlock>().Single().Text;
                return contenido.Contains("horario abierto de") ? contenido : null;
            },
            CatalogoDeTurnos.TimeoutPolling);

        using var json = JsonDocument.Parse(texto);
        json.RootElement.GetProperty("horario").GetString().Should().Be("horario abierto de 2 h");

        var retirado = await mcp.Cliente.CallToolAsync(
            "retirar_turno", new Dictionary<string, object?> { ["turno"] = nombre }, cancellationToken: ct);
        retirado.IsError.Should().NotBeTrue();
    }
}
