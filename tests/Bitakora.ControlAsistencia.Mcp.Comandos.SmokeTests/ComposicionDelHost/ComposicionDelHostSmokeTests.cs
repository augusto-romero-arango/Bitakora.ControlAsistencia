using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.ComposicionDelHost;

public class ComposicionDelHostSmokeTests(McpFixture mcp)
{
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ServidorMcp_MaterializaLaToolDeEjemplo_CuandoSeListanLasTools()
    {
        var ct = TestContext.Current.CancellationToken;
        var tools = await mcp.Cliente.ListToolsAsync(cancellationToken: ct);

        tools.Select(t => t.Name).Should().ContainSingle().Which.Should().Be("ejemplo_listar");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task EjemploListar_DeclaraFiltroNombreComoOpcional_CuandoSeLeeSuInputSchema()
    {
        var ct = TestContext.Current.CancellationToken;
        var tools = await mcp.Cliente.ListToolsAsync(cancellationToken: ct);
        var tool = tools.Single(t => t.Name == "ejemplo_listar");

        // Tipo explicito y no 'var': la rama '[]' necesita un tipo destino para compilar.
        List<string?> requeridas = tool.JsonSchema.TryGetProperty("required", out var required)
            ? [.. required.EnumerateArray().Select(e => e.GetString())]
            : [];

        requeridas.Should().NotContain("filtro_nombre");
    }

    // El hint viaja en _meta (McpMetadata) porque la extension 1.6.0 no soporta ToolAnnotations
    // del spec; cuando la extension exponga annotations.readOnlyHint, este test migra alli.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ServidorMcp_PublicaElHintDeSoloLecturaEnLaTool_CuandoSeListanLasTools()
    {
        var ct = TestContext.Current.CancellationToken;
        var tools = await mcp.Cliente.ListToolsAsync(cancellationToken: ct);
        var tool = tools.Single(t => t.Name == "ejemplo_listar");

        var meta = tool.ProtocolTool.Meta;
        meta.Should().NotBeNull("la tool debe publicar su _meta con el hint de solo lectura");
        meta!["readOnlyHint"]?.GetValue<bool>().Should().BeTrue("ejemplo_listar es de solo lectura");
    }
}
