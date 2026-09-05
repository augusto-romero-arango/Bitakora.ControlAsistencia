using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.SmokeTests.Fixtures;
using ModelContextProtocol.Client;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.SmokeTests.ComposicionDelHost;

// Cierra el CA-6 del issue #502: el registro que sirve tools/list (DefaultToolRegistry) vive en el
// paquete del HOST y resulto inalcanzable en unit tests -- ComposicionDelServidorTests solo pudo
// pinnear la metadata declarada por reflexion. Estos tests interrogan el catalogo VIVO que el host
// materializo en dev.
public class ComposicionDelHostSmokeTests(McpFixture mcp)
{
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ServidorMcp_MaterializaLasSeisToolsDeConsulta_CuandoSeListanLasTools()
    {
        var ct = TestContext.Current.CancellationToken;
        var tools = await mcp.Cliente.ListToolsAsync(cancellationToken: ct);

        tools.Select(t => t.Name).Should().BeEquivalentTo(
            "listar_turnos", "obtener_turno", "listar_sedes", "consultar_programacion",
            "listar_colaboradores", "buscar_colaboradores");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ConsultarProgramacion_DeclaraDesdeYHastaObligatorios_CuandoSeLeeSuInputSchema()
    {
        var ct = TestContext.Current.CancellationToken;
        var tools = await mcp.Cliente.ListToolsAsync(cancellationToken: ct);

        Requeridas(tools.Single(t => t.Name == "consultar_programacion"))
            .Should().BeEquivalentTo("desde", "hasta");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerTurno_DeclaraElIdObligatorio_CuandoSeLeeSuInputSchema()
    {
        var ct = TestContext.Current.CancellationToken;
        var tools = await mcp.Cliente.ListToolsAsync(cancellationToken: ct);

        Requeridas(tools.Single(t => t.Name == "obtener_turno"))
            .Should().BeEquivalentTo("id");
    }

    // CA-5 (issue #612): pin de required re-ejecutado -- listar_turnos sigue sin parametros
    // obligatorios (filtro_nombre es opcional) aunque el catalogo gano enConstruccion.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnos_NoDeclaraParametrosObligatorios_CuandoSeLeeSuInputSchema()
    {
        var ct = TestContext.Current.CancellationToken;
        var tools = await mcp.Cliente.ListToolsAsync(cancellationToken: ct);

        Requeridas(tools.Single(t => t.Name == "listar_turnos")).Should().BeEmpty();
    }

    // El hint viaja en _meta (McpMetadata) porque la extension 1.6.0 no soporta ToolAnnotations
    // del spec; cuando la extension exponga annotations.readOnlyHint, este test migra alli.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ServidorMcp_PublicaElHintDeSoloLecturaEnCadaTool_CuandoSeListanLasTools()
    {
        var ct = TestContext.Current.CancellationToken;
        var tools = await mcp.Cliente.ListToolsAsync(cancellationToken: ct);

        foreach (var tool in tools)
        {
            var meta = tool.ProtocolTool.Meta;
            meta.Should().NotBeNull($"la tool {tool.Name} debe publicar su _meta con el hint");
            meta!["readOnlyHint"]?.GetValue<bool>().Should().BeTrue(
                $"la tool {tool.Name} debe publicar readOnlyHint: true en _meta");
        }
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarColaboradores_NoDeclaraParametrosObligatorios_CuandoSeLeeSuInputSchema()
    {
        var ct = TestContext.Current.CancellationToken;
        var tools = await mcp.Cliente.ListToolsAsync(cancellationToken: ct);

        Requeridas(tools.Single(t => t.Name == "listar_colaboradores")).Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarColaboradores_DeclaraLosCuatroParametrosDelDiseno_CuandoSeLeeSuInputSchema()
    {
        var ct = TestContext.Current.CancellationToken;
        var tools = await mcp.Cliente.ListToolsAsync(cancellationToken: ct);

        Propiedades(tools.Single(t => t.Name == "listar_colaboradores")).Should().BeEquivalentTo(
            "identificacion", "sede", "etiquetas", "fecha_referencia");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task BuscarColaboradores_NoDeclaraParametrosObligatorios_CuandoSeLeeSuInputSchema()
    {
        var ct = TestContext.Current.CancellationToken;
        var tools = await mcp.Cliente.ListToolsAsync(cancellationToken: ct);

        Requeridas(tools.Single(t => t.Name == "buscar_colaboradores")).Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task BuscarColaboradores_DeclaraLosDosParametrosDelDiseno_CuandoSeLeeSuInputSchema()
    {
        var ct = TestContext.Current.CancellationToken;
        var tools = await mcp.Cliente.ListToolsAsync(cancellationToken: ct);

        Propiedades(tools.Single(t => t.Name == "buscar_colaboradores")).Should().BeEquivalentTo(
            "nombre", "identificaciones");
    }

    private static List<string?> Requeridas(McpClientTool tool) =>
        [.. tool.JsonSchema.GetProperty("required").EnumerateArray().Select(e => e.GetString())];

    private static List<string> Propiedades(McpClientTool tool) =>
        [.. tool.JsonSchema.GetProperty("properties").EnumerateObject().Select(p => p.Name)];
}
