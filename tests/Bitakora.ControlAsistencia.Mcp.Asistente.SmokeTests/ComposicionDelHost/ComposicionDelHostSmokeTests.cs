using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.ComposicionDelHost;

public class ComposicionDelHostSmokeTests(McpFixture mcp)
{
    private static readonly string[] ToolsEsperadas =
    [
        "obtener_sesion", "cerrar_sesion", "listar_sedes", "listar_colaboradores",
        "buscar_colaboradores", "registrar_sede", "registrar_colaborador",
        "listar_turnos", "obtener_turno", "crear_turno", "retirar_turno", "agregar_franja",
        "quitar_franja", "agregar_subfranja", "quitar_subfranja", "asignar_sede_franja",
        "listar_plantillas_semanales", "obtener_plantilla_semanal", "crear_plantilla_semanal",
        "retirar_plantilla_semanal", "asignar_turno_a_dia", "quitar_turno_de_dia"
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
        ],
        ["listar_turnos"] = [],
        ["obtener_turno"] = ["id"],
        ["crear_turno"] = ["nombre"],
        ["retirar_turno"] = ["turno"],
        ["agregar_franja"] = ["turno", "inicio", "fin"],
        ["quitar_franja"] = ["turno", "franja"],
        ["agregar_subfranja"] = ["turno", "franja", "tipo", "inicio", "fin"],
        ["quitar_subfranja"] = ["turno", "franja", "tipo", "inicio"],
        ["asignar_sede_franja"] = ["turno", "franja"],
        ["listar_plantillas_semanales"] = [],
        ["obtener_plantilla_semanal"] = ["plantilla"],
        ["crear_plantilla_semanal"] = ["nombre", "dias"],
        ["retirar_plantilla_semanal"] = ["plantilla"],
        ["asignar_turno_a_dia"] = ["plantilla", "turno", "dia"],
        ["quitar_turno_de_dia"] = ["plantilla", "dia"]
    };

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ServidorMcp_MaterializaLasVeintidosToolsMigradas_CuandoSeListanLasTools()
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

            var esEscritura = nombre is "registrar_sede" or "registrar_colaborador" or "crear_turno" or "retirar_turno"
                or "agregar_franja" or "quitar_franja" or "agregar_subfranja" or "quitar_subfranja"
                or "asignar_sede_franja" or "crear_plantilla_semanal" or "retirar_plantilla_semanal"
                or "asignar_turno_a_dia" or "quitar_turno_de_dia";
            (meta!["readOnlyHint"]?.GetValue<bool>()).Should().Be(!esEscritura, $"{nombre} debe publicar readOnlyHint");
        }
    }
}
