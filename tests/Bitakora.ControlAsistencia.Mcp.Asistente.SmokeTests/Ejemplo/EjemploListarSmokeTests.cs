using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Ejemplo;

public class EjemploListarSmokeTests(McpFixture mcp)
{
    // Recorre la cadena completa: host MCP -> worker -> HttpClient tipado -> Function App de
    // Programacion. Afirma la FORMA del contrato remodelado (Paso 3), no datos puntuales: un
    // entorno recien scaffoldeado puede no tener elementos cargados todavia.
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

    // Error path que no toca ningun dominio: la validacion de largo corta en el worker (Paso 3) y
    // responde el mensaje del .resx en produccion. Afirmar el texto exacto prueba que los recursos
    // embebidos viajaron en el publish (un GetString nulo o un .resx ausente daria otro texto).
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

    // Camino valido de fecha_referencia (MEF-ADR-0048 seccion 2 verificacion 3): la extension MCP
    // coerciona todo string con forma de fecha antes de que la tool lo reciba, y
    // ArgumentosCrudosMcpMiddleware lo restaura (Paso 6, "Estado de este scaffold"). El eco exacto
    // de la fecha es la unica deteccion e2e de que ese middleware sigue activo -- ni esta tool call
    // omitiendo el parametro ni el error path de arriba ejercitan esa restauracion.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task EjemploListar_DevuelveLaFechaDeReferenciaIntacta_CuandoLaFechaEsValida()
    {
        var ct = TestContext.Current.CancellationToken;
        var resultado = await mcp.Cliente.CallToolAsync(
            "ejemplo_listar",
            new Dictionary<string, object?> { ["fecha_referencia"] = "2026-09-01" },
            cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        var texto = resultado.Content.OfType<TextContentBlock>().Single().Text;

        using var json = JsonDocument.Parse(texto);
        var raiz = json.RootElement;
        var mostrando = raiz.GetProperty("mostrando").GetInt32();

        raiz.GetProperty("fechaReferencia").GetString().Should().Be("2026-09-01");
        raiz.GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(mostrando);
        raiz.GetProperty("elementos").EnumerateArray().ToList().Should().HaveCount(mostrando);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task EjemploListar_RespondeElMensajeDeValidacion_CuandoLaFechaDeReferenciaEsInvalida()
    {
        var ct = TestContext.Current.CancellationToken;
        var resultado = await mcp.Cliente.CallToolAsync(
            "ejemplo_listar",
            new Dictionary<string, object?> { ["fecha_referencia"] = "2026-99-99" },
            cancellationToken: ct);

        resultado.Content.OfType<TextContentBlock>().Single().Text
            .Should().Be("'fecha_referencia' debe tener formato yyyy-MM-dd; llego '2026-99-99'.");
    }
}
