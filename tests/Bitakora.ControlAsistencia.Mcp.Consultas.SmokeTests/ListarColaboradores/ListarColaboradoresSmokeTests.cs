using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.SmokeTests.ListarColaboradores;

public class ListarColaboradoresSmokeTests(McpFixture mcp)
{
    // Recorre la cadena completa: host MCP -> worker -> HttpClient tipado -> dominio Colaboradores
    // -> Marten. Los datos son los reales de dev, asi que se afirma la forma del remodelado
    // (issue #530), no valores puntuales.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarColaboradores_DevuelveElCatalogoCompactoConDatosReales_CuandoSeInvocaSinFiltro()
    {
        var ct = TestContext.Current.CancellationToken;
        var resultado = await mcp.Cliente.CallToolAsync(
            "listar_colaboradores", new Dictionary<string, object?>(), cancellationToken: ct);

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

    // CA-2 (issue #586): fecha_referencia con forma de fecha llegaba coercionada a DateTimeOffset
    // reformateado antes de ArgumentosCrudosMcpMiddleware, y el worker respondia siempre el mensaje
    // FechaInvalida. Ninguna tool call de esta suite ejercitaba el camino con fecha VALIDA (la de
    // arriba omite el filtro, la de abajo manda una fecha invalida): ese gap oculto el defecto.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarColaboradores_DevuelveElCatalogoCompacto_CuandoFechaReferenciaEsValida()
    {
        var ct = TestContext.Current.CancellationToken;
        var resultado = await mcp.Cliente.CallToolAsync(
            "listar_colaboradores",
            new Dictionary<string, object?> { ["fecha_referencia"] = "2026-09-01" },
            cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        var texto = resultado.Content.OfType<TextContentBlock>().Single().Text;

        using var json = JsonDocument.Parse(texto);
        var raiz = json.RootElement;

        var mostrando = raiz.GetProperty("mostrando").GetInt32();
        raiz.GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(mostrando);
        raiz.GetProperty("colaboradores").EnumerateArray().ToList().Should().HaveCount(mostrando);
    }

    // Error path que NO toca el dominio: la validacion de fecha corta en el worker y responde el
    // mensaje del .resx en produccion, igual que ConsultarProgramacion_RespondeElMensajeDeValidacion.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarColaboradores_RespondeElMensajeDeValidacion_CuandoLaFechaReferenciaEsInvalida()
    {
        var ct = TestContext.Current.CancellationToken;
        var resultado = await mcp.Cliente.CallToolAsync(
            "listar_colaboradores",
            new Dictionary<string, object?> { ["fecha_referencia"] = "2026-99-99" },
            cancellationToken: ct);

        resultado.Content.OfType<TextContentBlock>().Single().Text
            .Should().Be("'fecha_referencia' debe tener formato yyyy-MM-dd; llego '2026-99-99'.");
    }
}
