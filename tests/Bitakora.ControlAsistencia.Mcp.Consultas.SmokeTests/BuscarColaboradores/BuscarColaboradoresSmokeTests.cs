using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.SmokeTests.BuscarColaboradores;

public class BuscarColaboradoresSmokeTests(McpFixture mcp)
{
    // Recorre la cadena completa: host MCP -> worker -> HttpClient tipado -> dominio Colaboradores
    // -> Marten. "test" es un token real: las suites de Colaboradores registran colaboradores
    // [TEST] ... en dev y TokenizarNombre (#587) separa por todo caracter no alfanumerico. Se
    // afirma la forma del remodelado (issue #588), no valores puntuales -- los datos de dev
    // cambian entre corridas.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task BuscarColaboradores_DevuelveLasCoincidenciasConDatosReales_CuandoSeBuscaPorNombre()
    {
        var ct = TestContext.Current.CancellationToken;
        var resultado = await mcp.Cliente.CallToolAsync(
            "buscar_colaboradores",
            new Dictionary<string, object?> { ["nombre"] = "test" },
            cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        var texto = resultado.Content.OfType<TextContentBlock>().Single().Text;

        using var json = JsonDocument.Parse(texto);
        var raiz = json.RootElement;

        var mostrando = raiz.GetProperty("mostrando").GetInt32();
        raiz.GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(mostrando);

        var colaboradores = raiz.GetProperty("colaboradores").EnumerateArray().ToList();
        colaboradores.Should().HaveCount(mostrando);

        foreach (var colaborador in colaboradores)
        {
            colaborador.GetProperty("identificacion").GetString().Should().NotBeNullOrWhiteSpace();
            colaborador.GetProperty("nombre").GetString().Should().NotBeNullOrWhiteSpace();
        }
    }

    // Error path que NO toca el dominio: la validacion de criterio corta en el worker y responde
    // el mensaje del .resx en produccion. Afirmar el texto exacto prueba que los recursos
    // embebidos viajaron en el publish.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task BuscarColaboradores_RespondeElMensajeDeValidacion_CuandoNoHayNombreNiIdentificaciones()
    {
        var ct = TestContext.Current.CancellationToken;
        var resultado = await mcp.Cliente.CallToolAsync(
            "buscar_colaboradores", new Dictionary<string, object?>(), cancellationToken: ct);

        resultado.Content.OfType<TextContentBlock>().Single().Text
            .Should().Be(
                "Indica un nombre (palabras completas) o una o varias identificaciones para buscar.");
    }
}
