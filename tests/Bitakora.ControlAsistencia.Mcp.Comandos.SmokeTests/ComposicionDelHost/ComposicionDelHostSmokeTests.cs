using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.ComposicionDelHost;

public class ComposicionDelHostSmokeTests(McpFixture mcp)
{
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ServidorMcp_MaterializaLaToolRegistrarSede_CuandoSeListanLasTools()
    {
        var ct = TestContext.Current.CancellationToken;
        var tools = await mcp.Cliente.ListToolsAsync(cancellationToken: ct);

        tools.Select(t => t.Name).Should().ContainSingle().Which.Should().Be("registrar_sede");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarSede_DeclaraCodigoYNombreObligatorios_CuandoSeLeeSuInputSchema()
    {
        var ct = TestContext.Current.CancellationToken;
        var tools = await mcp.Cliente.ListToolsAsync(cancellationToken: ct);
        var tool = tools.Single(t => t.Name == "registrar_sede");

        var requeridas = tool.JsonSchema.GetProperty("required")
            .EnumerateArray().Select(e => e.GetString());

        requeridas.Should().BeEquivalentTo("codigo", "nombre");
    }

    // El hint viaja en _meta (McpMetadata) porque la extension 1.6.0 no soporta ToolAnnotations
    // del spec; cuando la extension exponga annotations.readOnlyHint, este test migra alli.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ServidorMcp_PublicaElHintDeEscrituraEnLaTool_CuandoSeListanLasTools()
    {
        var ct = TestContext.Current.CancellationToken;
        var tools = await mcp.Cliente.ListToolsAsync(cancellationToken: ct);
        var tool = tools.Single(t => t.Name == "registrar_sede");

        var meta = tool.ProtocolTool.Meta;
        meta.Should().NotBeNull("la tool debe publicar su _meta con los hints");
        meta!["readOnlyHint"]?.GetValue<bool>().Should().BeFalse("registrar_sede escribe en el dominio");
        meta["destructiveHint"]?.GetValue<bool>().Should().BeFalse("registrar_sede no destruye datos");
    }
}
