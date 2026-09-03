using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.SmokeTests.ConsultarProgramacion;

public class ConsultarProgramacionSmokeTests(McpFixture mcp)
{
    // CA-3 (issue #586): primera tool call valida de consultar_programacion (MEF-ADR-0048 seccion 2
    // verificacion 3). Antes del fix, desde/hasta con forma de fecha llegaban coercionados a
    // DateTimeOffset reformateado y el worker respondia siempre FechaInvalida -- la tool principal
    // de Consultas no podia responder ninguna consulta con fechas. Se afirma forma, no datos
    // puntuales: los datos de dev cambian entre corridas.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ConsultarProgramacion_DevuelveLaProgramacionRemodelada_CuandoDesdeYHastaSonValidos()
    {
        var ct = TestContext.Current.CancellationToken;
        var resultado = await mcp.Cliente.CallToolAsync(
            "consultar_programacion",
            new Dictionary<string, object?> { ["desde"] = "2026-09-01", ["hasta"] = "2026-09-07" },
            cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        var texto = resultado.Content.OfType<TextContentBlock>().Single().Text;

        using var json = JsonDocument.Parse(texto);
        var raiz = json.RootElement;

        raiz.TryGetProperty("desde", out _).Should().BeTrue();
        raiz.TryGetProperty("hasta", out _).Should().BeTrue();
        raiz.TryGetProperty("total", out _).Should().BeTrue();
        raiz.TryGetProperty("mostrando", out _).Should().BeTrue();
        raiz.TryGetProperty("turnos", out var turnos).Should().BeTrue();
        turnos.ValueKind.Should().Be(JsonValueKind.Array);
    }

    // Error path que NO toca los dominios: la validacion de fecha corta en el worker y responde
    // el mensaje del .resx en produccion. Afirmar el texto exacto prueba que los recursos
    // embebidos viajaron en el publish (un GetString nulo o un resx ausente daria otro texto).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ConsultarProgramacion_RespondeElMensajeDeValidacion_CuandoLaFechaEsInvalida()
    {
        var ct = TestContext.Current.CancellationToken;
        var resultado = await mcp.Cliente.CallToolAsync(
            "consultar_programacion",
            new Dictionary<string, object?> { ["desde"] = "2026-99-99", ["hasta"] = "2026-01-01" },
            cancellationToken: ct);

        resultado.Content.OfType<TextContentBlock>().Single().Text
            .Should().Be("'desde' debe ser una fecha con formato yyyy-MM-dd; llego '2026-99-99'.");
    }
}
