using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.SmokeTests.ListarTurnos;

public class ListarTurnosSmokeTests(McpFixture mcp)
{
    // Recorre la cadena completa: host MCP -> worker -> HttpClient tipado -> dominio Programacion
    // -> Marten. La forma compacta (total/mostrando/turnos con id+nombre+horario) es el contrato
    // remodelado del issue #502; los datos son los reales de dev, asi que se afirma la forma y la
    // consistencia, no valores puntuales.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnos_DevuelveElCatalogoCompactoConDatosReales_CuandoSeInvocaSinFiltro()
    {
        var ct = TestContext.Current.CancellationToken;
        var resultado = await mcp.Cliente.CallToolAsync(
            "listar_turnos", new Dictionary<string, object?>(), cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        var texto = resultado.Content.OfType<TextContentBlock>().Single().Text;

        using var json = JsonDocument.Parse(texto);
        var raiz = json.RootElement;

        var total = raiz.GetProperty("total").GetInt32();
        var turnos = raiz.GetProperty("turnos").EnumerateArray().ToList();

        total.Should().BeGreaterThan(0, "dev tiene turnos reales cargados");
        turnos.Should().HaveCount(raiz.GetProperty("mostrando").GetInt32());

        foreach (var turno in turnos)
        {
            turno.GetProperty("id").GetString().Should().NotBeNullOrWhiteSpace();
            turno.GetProperty("nombre").GetString().Should().NotBeNullOrWhiteSpace();
            turno.GetProperty("horario").GetString().Should().NotBeNullOrWhiteSpace();
        }
    }
}
