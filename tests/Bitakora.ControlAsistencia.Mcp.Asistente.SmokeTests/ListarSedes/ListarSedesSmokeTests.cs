using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.ListarSedes;

public class ListarSedesSmokeTests(McpFixture mcp)
{
    // Siembra su propia sede (registrar_sede) y espera a que la proyeccion async la materialice.
    // Verificacion canonica 3 de MEF-ADR-0048 seccion 2: tool call de lectura real contra dev.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarSedes_DevuelveLaSedeSembrada_CuandoSeInvocaSinFiltro()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigo = await Sembrado.RegistrarSedeAsync(mcp.Cliente, ct);

        var json = await Polling.WaitUntilAsync(async () =>
        {
            var resultado = await mcp.Cliente.CallToolAsync(
                "listar_sedes", new Dictionary<string, object?>(), cancellationToken: ct);
            var documento = Sembrado.LeerJson(resultado);
            var contiene = documento.RootElement.GetProperty("sedes").EnumerateArray()
                .Any(s => s.GetProperty("codigo").GetString() == codigo);
            return contiene ? documento : null;
        }, TimeSpan.FromSeconds(60));

        var raiz = json.RootElement;
        raiz.GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        foreach (var sede in raiz.GetProperty("sedes").EnumerateArray())
        {
            sede.GetProperty("codigo").GetString().Should().NotBeNullOrWhiteSpace();
            sede.GetProperty("nombre").GetString().Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarSedes_AcotaElCatalogo_CuandoElFiltroNoCoincideConNingunaSede()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "listar_sedes",
            new Dictionary<string, object?> { ["filtro_nombre"] = $"zzz-{Guid.CreateVersion7():N}" },
            cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        using var json = Sembrado.LeerJson(resultado);
        json.RootElement.GetProperty("total").GetInt32().Should().Be(0);
    }
}
