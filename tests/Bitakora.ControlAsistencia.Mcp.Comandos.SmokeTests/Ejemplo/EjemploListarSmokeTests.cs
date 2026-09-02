using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.Ejemplo;

public class EjemploListarSmokeTests(McpFixture mcp)
{
    // Recorre la cadena completa: host MCP -> worker -> HttpClient tipado -> Function App de
    // Programacion. Afirma la FORMA del contrato remodelado, no datos puntuales: un tenant sin
    // turnos cargados puede devolver el catalogo vacio.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task EjemploListar_DevuelveElCatalogoConLaFormaEsperada_CuandoSeInvocaSinFiltro()
    {
        var ct = TestContext.Current.CancellationToken;
        var resultado = await mcp.Cliente.CallToolAsync(
            "ejemplo_listar", new Dictionary<string, object?>(), cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        var texto = resultado.Content.OfType<TextContentBlock>().Single().Text;

        using var json = JsonDocument.Parse(texto);
        var raiz = json.RootElement;

        var mostrando = raiz.GetProperty("mostrando").GetInt32();
        var elementos = raiz.GetProperty("elementos").EnumerateArray().ToList();

        elementos.Should().HaveCount(mostrando);
        foreach (var elemento in elementos)
        {
            elemento.GetProperty("id").GetString().Should().NotBeNullOrWhiteSpace();
            elemento.GetProperty("nombre").GetString().Should().NotBeNullOrWhiteSpace();
        }
    }

    // Error path que no toca ningun dominio: la validacion de largo corta en el worker y responde
    // el mensaje del .resx en produccion. Afirmar el texto exacto prueba que los recursos
    // embebidos viajaron en el publish.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task EjemploListar_RespondeElMensajeDeValidacion_CuandoElFiltroExcedeElLargoMaximo()
    {
        var ct = TestContext.Current.CancellationToken;
        var filtroDemasiadoLargo = new string('a', 101);

        var resultado = await mcp.Cliente.CallToolAsync(
            "ejemplo_listar",
            new Dictionary<string, object?> { ["filtro_nombre"] = filtroDemasiadoLargo },
            cancellationToken: ct);

        resultado.Content.OfType<TextContentBlock>().Single().Text
            .Should().Be("El filtro no puede superar 100 caracteres.");
    }
}
