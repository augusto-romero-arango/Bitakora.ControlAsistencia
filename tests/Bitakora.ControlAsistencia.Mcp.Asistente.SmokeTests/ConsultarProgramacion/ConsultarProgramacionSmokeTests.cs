using System.Globalization;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.ConsultarProgramacion;

public class ConsultarProgramacionSmokeTests(McpFixture mcp)
{
    // Tool call valida de consultar_programacion (MEF-ADR-0048 seccion 2 verificacion 3): desde/hasta
    // con forma de fecha no deben llegar coercionados al worker (responderia FechaInvalida). Se afirma
    // forma, no datos puntuales: los datos de dev cambian entre corridas.
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
        if (!texto.StartsWith('{'))
        {
            DebeSerLaFraseDeVacio(texto);
            return;
        }

        using var json = JsonDocument.Parse(texto);
        var raiz = json.RootElement;

        // El dominio devuelve el rango APLICADO, que puede diferir del pedido si hubo recorte (lo
        // senala en "nota"): se afirma que son fechas en el formato del contrato, no que sean las
        // pedidas.
        DateOnly.TryParseExact(raiz.GetProperty("desde").GetString()!, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out _).Should().BeTrue();
        DateOnly.TryParseExact(raiz.GetProperty("hasta").GetString()!, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out _).Should().BeTrue();

        raiz.GetProperty("dias").EnumerateArray().ToList().Should().HaveCount(raiz.GetProperty("mostrando").GetInt32());
        raiz.TryGetProperty("total", out _).Should().BeFalse("la ventana temporal no reporta total (CA-ADR-0039)");

        raiz.GetProperty("dias").EnumerateArray()
            .SelectMany(d => d.GetProperty("bloques").EnumerateArray())
            .Select(b => b.GetString()!)
            .Should().OnlyContain(b => !b.Contains("(+"));
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
                ["codigos_colaborador"] = "COL-1"
            },
            cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        var texto = resultado.Content.OfType<TextContentBlock>().Single().Text;
        if (!texto.StartsWith('{'))
        {
            DebeSerLaFraseDeVacio(texto);
            return;
        }

        using var json = JsonDocument.Parse(texto);
        json.RootElement.GetProperty("dias").EnumerateArray()
            .Select(t => t.GetProperty("colaborador").GetString())
            .Should().AllBe("COL-1");
    }

    private async Task<string> CodigoDeUnaSedeExistente(CancellationToken ct)
    {
        var sedes = await mcp.Cliente.CallToolAsync(
            "listar_sedes", new Dictionary<string, object?>(), cancellationToken: ct);
        using var json = JsonDocument.Parse(sedes.Content.OfType<TextContentBlock>().Single().Text);
        return json.RootElement.GetProperty("sedes").EnumerateArray().First().GetProperty("codigo").GetString()!;
    }

    private async Task AfirmaSobreOFrase(string parametro, string valor, CancellationToken ct)
    {
        var resultado = await mcp.Cliente.CallToolAsync(
            "consultar_programacion",
            new Dictionary<string, object?>
            {
                ["desde"] = "2026-09-01",
                ["hasta"] = "2026-09-07",
                [parametro] = valor
            },
            cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        var texto = resultado.Content.OfType<TextContentBlock>().Single().Text;
        if (texto.StartsWith('{'))
        {
            using var json = JsonDocument.Parse(texto);
            json.RootElement.TryGetProperty("dias", out _).Should().BeTrue();
            json.RootElement.TryGetProperty("total", out _).Should().BeFalse();
        }
        else
        {
            DebeSerLaFraseDeVacio(texto);
        }
    }

    // Sin dias (o sin colaboradores en el grupo) la tool responde una frase con el periodo; un
    // rechazo del dominio tambien es texto, asi que se distingue por la forma de la frase.
    private static void DebeSerLaFraseDeVacio(string texto) =>
        texto.Should().StartWith("No hay ").And.Contain("2026-09-01");

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ConsultarProgramacion_AceptaElSobreOLaFrase_CuandoSeFiltraPorSede()
    {
        var ct = TestContext.Current.CancellationToken;
        await AfirmaSobreOFrase("sede", await CodigoDeUnaSedeExistente(ct), ct);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ConsultarProgramacion_AceptaElSobreOLaFrase_CuandoSeFiltraPorSedeTrabajo()
    {
        var ct = TestContext.Current.CancellationToken;
        await AfirmaSobreOFrase("sede_trabajo", await CodigoDeUnaSedeExistente(ct), ct);
    }

    // Validacion de entrada barata (CA-ADR-0041 decision 3): el cursor viaja tal cual hasta
    // turnos-vigentes, que rechaza uno que no decodifica.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ConsultarProgramacion_RespondeElRechazoDelDominio_CuandoElCursorEsInvalido()
    {
        var ct = TestContext.Current.CancellationToken;
        var resultado = await mcp.Cliente.CallToolAsync(
            "consultar_programacion",
            new Dictionary<string, object?>
            {
                ["desde"] = "2026-09-01",
                ["hasta"] = "2026-09-07",
                ["cursor"] = "no-es-un-cursor"
            },
            cancellationToken: ct);

        resultado.Content.OfType<TextContentBlock>().Single().Text
            .Should().StartWith("El dominio rechazo la consulta:").And.Contain("cursor");
    }

    // Error path que NO toca los dominios: el texto exacto del .resx prueba que los recursos embebidos
    // viajaron en el publish (un GetString nulo o un resx ausente daria otro texto).
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
