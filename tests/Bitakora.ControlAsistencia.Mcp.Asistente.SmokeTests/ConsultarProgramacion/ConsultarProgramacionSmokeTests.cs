using System.Globalization;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.ConsultarProgramacion;

public class ConsultarProgramacionSmokeTests(McpFixture mcp)
{
    // Primera tool call valida de consultar_programacion (MEF-ADR-0048 seccion 2
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

        // El dominio devuelve el rango APLICADO, que puede diferir del pedido si hubo recorte (lo
        // senala en "nota"): se afirma que son fechas en el formato del contrato, no que sean las
        // pedidas.
        DateOnly.TryParseExact(raiz.GetProperty("desde").GetString()!, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out _).Should().BeTrue();
        DateOnly.TryParseExact(raiz.GetProperty("hasta").GetString()!, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out _).Should().BeTrue();

        var mostrando = raiz.GetProperty("mostrando").GetInt32();
        raiz.GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(mostrando);
        raiz.GetProperty("turnos").EnumerateArray().ToList().Should().HaveCount(mostrando);
    }

    // MEF-ADR-0048 seccion 2 verificacion 3: los parametros identificador opcionales tambien
    // necesitan su tool call valida. Se afirma el filtro aplicado, no datos puntuales.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ConsultarProgramacion_SoloDevuelveDiasDelColaborador_CuandoSeFiltraPorCodigoColaborador()
    {
        var ct = TestContext.Current.CancellationToken;
        var resultado = await mcp.Cliente.CallToolAsync(
            "consultar_programacion",
            new Dictionary<string, object?>
            {
                ["desde"] = "2026-09-01",
                ["hasta"] = "2026-09-07",
                ["codigo_colaborador"] = "COL-1"
            },
            cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        using var json = JsonDocument.Parse(resultado.Content.OfType<TextContentBlock>().Single().Text);
        json.RootElement.GetProperty("turnos").EnumerateArray()
            .Select(t => t.GetProperty("colaborador").GetString())
            .Should().AllBe("COL-1");
    }

    // sede_id con forma GUID: la extension lo coerciona y ArgumentosCrudosMcpMiddleware debe
    // restaurarlo; una sede inexistente responde la programacion vacia, nunca un error.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ConsultarProgramacion_DevuelveProgramacionVacia_CuandoSeFiltraPorUnaSedeSinBloques()
    {
        var ct = TestContext.Current.CancellationToken;
        var resultado = await mcp.Cliente.CallToolAsync(
            "consultar_programacion",
            new Dictionary<string, object?>
            {
                ["desde"] = "2026-09-01",
                ["hasta"] = "2026-09-07",
                ["sede_id"] = Guid.CreateVersion7().ToString()
            },
            cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        using var json = JsonDocument.Parse(resultado.Content.OfType<TextContentBlock>().Single().Text);
        json.RootElement.GetProperty("total").GetInt32().Should().Be(0);
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
