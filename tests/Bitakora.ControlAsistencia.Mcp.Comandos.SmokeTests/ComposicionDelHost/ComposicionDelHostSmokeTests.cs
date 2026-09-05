using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.ComposicionDelHost;

public class ComposicionDelHostSmokeTests(McpFixture mcp)
{
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ServidorMcp_MaterializaElCatalogoDeTools_CuandoSeListanLasTools()
    {
        var ct = TestContext.Current.CancellationToken;
        var tools = await mcp.Cliente.ListToolsAsync(cancellationToken: ct);

        tools.Select(t => t.Name).Should().BeEquivalentTo(
            "registrar_sede", "registrar_colaborador", "solicitar_programacion_turno",
            "crear_turno", "retirar_turno");
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

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarColaborador_DeclaraLosSeisRequeridos_CuandoSeLeeSuInputSchema()
    {
        var ct = TestContext.Current.CancellationToken;
        var tools = await mcp.Cliente.ListToolsAsync(cancellationToken: ct);
        var tool = tools.Single(t => t.Name == "registrar_colaborador");

        var requeridas = tool.JsonSchema.GetProperty("required")
            .EnumerateArray().Select(e => e.GetString());

        requeridas.Should().BeEquivalentTo(
            "tipo_identificacion", "numero_identificacion", "primer_nombre",
            "primer_apellido", "codigo_colaborador", "fecha_inicio");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_DeclaraLosCincoRequeridos_CuandoSeLeeSuInputSchema()
    {
        var ct = TestContext.Current.CancellationToken;
        var tools = await mcp.Cliente.ListToolsAsync(cancellationToken: ct);
        var tool = tools.Single(t => t.Name == "solicitar_programacion_turno");

        var requeridas = tool.JsonSchema.GetProperty("required")
            .EnumerateArray().Select(e => e.GetString());

        requeridas.Should().BeEquivalentTo(
            "desde", "hasta", "turno", "sede_de_programacion", "identificaciones");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearTurno_DeclaraNombreObligatorioYEsDescansoOpcional_CuandoSeLeeSuInputSchema()
    {
        var ct = TestContext.Current.CancellationToken;
        var tools = await mcp.Cliente.ListToolsAsync(cancellationToken: ct);
        var tool = tools.Single(t => t.Name == "crear_turno");

        var requeridas = tool.JsonSchema.GetProperty("required")
            .EnumerateArray().Select(e => e.GetString());

        requeridas.Should().BeEquivalentTo("nombre");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarTurno_DeclaraTurnoObligatorio_CuandoSeLeeSuInputSchema()
    {
        var ct = TestContext.Current.CancellationToken;
        var tools = await mcp.Cliente.ListToolsAsync(cancellationToken: ct);
        var tool = tools.Single(t => t.Name == "retirar_turno");

        var requeridas = tool.JsonSchema.GetProperty("required")
            .EnumerateArray().Select(e => e.GetString());

        requeridas.Should().BeEquivalentTo("turno");
    }

    // El hint viaja en _meta (McpMetadata) porque la extension 1.6.0 no soporta ToolAnnotations
    // del spec; cuando la extension exponga annotations.readOnlyHint, este test migra alli.
    // Recorre TODO el catalogo, no una tool por nombre: MEF-ADR-0048 seccion 2 (verificacion 2,
    // componente 3) exige el pin del hint para toda tool, y la seccion 6 cuenta con que una tool
    // nueva lo hereda por esta via -- acotar el assert a un nombre rompe esa herencia en silencio.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ServidorMcp_PublicaElHintDeEscrituraEnCadaTool_CuandoSeListanLasTools()
    {
        var ct = TestContext.Current.CancellationToken;
        var tools = await mcp.Cliente.ListToolsAsync(cancellationToken: ct);

        foreach (var tool in tools)
        {
            var meta = tool.ProtocolTool.Meta;
            var esDestructiva = tool.Name == "retirar_turno";
            meta.Should().NotBeNull($"{tool.Name} debe publicar su _meta con los hints");
            meta!["readOnlyHint"]?.GetValue<bool>().Should().BeFalse($"{tool.Name} escribe en el dominio");
            meta["destructiveHint"]?.GetValue<bool>().Should().Be(
                esDestructiva, $"{tool.Name} destructiveHint debe ser {esDestructiva}");
        }
    }
}
