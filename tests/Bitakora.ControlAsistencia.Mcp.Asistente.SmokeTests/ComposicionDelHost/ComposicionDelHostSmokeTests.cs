using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.ComposicionDelHost;

public class ComposicionDelHostSmokeTests(McpFixture mcp)
{
    private static readonly string[] ToolsEsperadas =
    [
        "obtener_sesion", "cerrar_sesion", "listar_sedes", "listar_colaboradores",
        "buscar_colaboradores", "registrar_sede", "registrar_colaborador"
    ];

    private static readonly Dictionary<string, string[]> RequeridasPorTool = new()
    {
        ["obtener_sesion"] = [],
        ["cerrar_sesion"] = [],
        ["listar_sedes"] = [],
        ["listar_colaboradores"] = [],
        ["buscar_colaboradores"] = [],
        ["registrar_sede"] = ["codigo", "nombre"],
        ["registrar_colaborador"] =
        [
            "tipo_identificacion", "numero_identificacion", "primer_nombre",
            "primer_apellido", "codigo_colaborador", "fecha_inicio"
        ]
    };

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ServidorMcp_MaterializaLasSieteToolsMigradas_CuandoSeListanLasTools()
    {
        var ct = TestContext.Current.CancellationToken;
        var tools = await mcp.Cliente.ListToolsAsync(cancellationToken: ct);

        tools.Select(t => t.Name).Should().BeEquivalentTo(ToolsEsperadas);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ServidorMcp_PineaElRequiredDelInputSchemaDeCadaTool_CuandoSeLeeElCatalogo()
    {
        var ct = TestContext.Current.CancellationToken;
        var tools = await mcp.Cliente.ListToolsAsync(cancellationToken: ct);

        foreach (var (nombre, esperadas) in RequeridasPorTool)
        {
            var tool = tools.Single(t => t.Name == nombre);

            List<string?> requeridas = tool.JsonSchema.TryGetProperty("required", out var required)
                ? [.. required.EnumerateArray().Select(e => e.GetString())]
                : [];

            requeridas.Should().BeEquivalentTo(esperadas, $"el required de {nombre} no cambia en el traslado");
        }
    }

    // El hint viaja en _meta (McpMetadata) porque la extension 1.6.0 no soporta ToolAnnotations.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ServidorMcp_PublicaLosHintsDeLasTools_CuandoSeListanLasTools()
    {
        var ct = TestContext.Current.CancellationToken;
        var tools = await mcp.Cliente.ListToolsAsync(cancellationToken: ct);

        foreach (var nombre in ToolsEsperadas)
        {
            var meta = tools.Single(t => t.Name == nombre).ProtocolTool.Meta;
            meta.Should().NotBeNull($"{nombre} debe publicar su _meta");

            var esEscritura = nombre is "registrar_sede" or "registrar_colaborador";
            meta!["readOnlyHint"]?.GetValue<bool>().Should().Be(!esEscritura);
        }
    }
}
