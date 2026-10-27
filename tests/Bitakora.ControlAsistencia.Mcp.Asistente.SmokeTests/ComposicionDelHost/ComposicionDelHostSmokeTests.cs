using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.ComposicionDelHost;

public class ComposicionDelHostSmokeTests(McpFixture mcp)
{
    private static readonly string[] ToolsEsperadas =
    [
        "obtener_sesion", "cerrar_sesion", "listar_sedes", "listar_colaboradores",
        "buscar_colaboradores", "buscar_sedes", "registrar_sede", "registrar_colaborador", "registrar_colaboradores",
        "buscar_turnos", "listar_turnos", "obtener_turno", "crear_turno", "retirar_turno", "agregar_franja",
        "quitar_franja", "agregar_subfranja", "quitar_subfranja", "asignar_sede_franja",
        "buscar_plantillas_semanales", "listar_plantillas_semanales", "obtener_plantilla_semanal",
        "crear_plantilla_semanal", "buscar_plantillas", "listar_plantillas", "obtener_plantilla",
        "retirar_plantilla_semanal", "asignar_turno_a_dia", "quitar_turno_de_dia",
        "consultar_programacion", "consultar_ausencias", "consultar_advertencias_programacion",
        "consultar_malla_turnos", "consultar_dias_sin_programar",
        "solicitar_programacion_turno",
        "solicitar_programacion_turno_por_grupo", "aplicar_plantilla_semanal", "aplicar_plantilla_semanal_por_grupo",
        "programar_ausencia", "cancelar_ausencia",
        "listar_jornadas", "crear_jornada", "modificar_limites_jornada", "asignar_jornada_predeterminada",
        "asignar_jornada_a_plantilla_semanal", "quitar_jornada_de_plantilla_semanal",
        "asignar_jornada", "asignar_jornada_por_grupo", "desactivar_jornada",
        "modificar_ventana_de_entrada", "crear_turno_de_horario_abierto",
        "asignar_etiqueta", "retirar_etiqueta"
    ];

    private static readonly Dictionary<string, string[]> RequeridasPorTool = new()
    {
        ["obtener_sesion"] = [],
        ["cerrar_sesion"] = [],
        ["listar_sedes"] = [],
        ["listar_colaboradores"] = [],
        ["buscar_colaboradores"] = [],
        ["registrar_sede"] = ["codigo", "nombre"],
        ["registrar_colaboradores"] = ["colaboradores"],
        ["registrar_colaborador"] =
        [
            "tipo_identificacion", "numero_identificacion", "primer_nombre",
            "primer_apellido", "codigo_colaborador", "fecha_inicio"
        ],
        ["buscar_sedes"] = [],
        ["buscar_turnos"] = [],
        ["listar_turnos"] = [],
        ["obtener_turno"] = ["turno_id"],
        ["crear_turno"] = ["nombre"],
        ["retirar_turno"] = ["turno_id"],
        ["agregar_franja"] = ["turno_id", "inicio", "fin"],
        ["quitar_franja"] = ["turno_id", "franja"],
        ["agregar_subfranja"] = ["turno_id", "franja", "tipo", "inicio", "fin"],
        ["quitar_subfranja"] = ["turno_id", "franja", "tipo", "inicio"],
        ["asignar_sede_franja"] = ["turno_id", "franja"],
        ["buscar_plantillas_semanales"] = [],
        ["listar_plantillas_semanales"] = [],
        ["obtener_plantilla_semanal"] = ["plantilla_id"],
        ["buscar_plantillas"] = [],
        ["listar_plantillas"] = [],
        ["obtener_plantilla"] = ["plantilla_id"],
        ["crear_plantilla_semanal"] = ["nombre", "dias"],
        ["retirar_plantilla_semanal"] = ["plantilla_id"],
        ["asignar_turno_a_dia"] = ["plantilla_id", "turno_id", "dia"],
        ["quitar_turno_de_dia"] = ["plantilla_id", "dia"],
        ["consultar_programacion"] = ["desde", "hasta"],
        ["consultar_ausencias"] = ["desde", "hasta"],
        ["consultar_advertencias_programacion"] = [],
        ["consultar_malla_turnos"] = [],
        ["consultar_dias_sin_programar"] = [],
        ["solicitar_programacion_turno"] = ["desde", "hasta", "turno_id", "identificaciones"],
        ["solicitar_programacion_turno_por_grupo"] = ["desde", "hasta", "turno_id"],
        ["aplicar_plantilla_semanal"] = ["desde", "hasta", "plantilla_id", "identificaciones"],
        ["aplicar_plantilla_semanal_por_grupo"] = ["desde", "hasta", "plantilla_id"],
        ["programar_ausencia"] = ["identificacion", "desde", "hasta", "motivo"],
        ["cancelar_ausencia"] = ["identificacion", "desde", "hasta"],
        ["listar_jornadas"] = [],
        ["crear_jornada"] = ["horas_semanales", "tope_diario", "minimo_diario", "dias_descanso_por_semana"],
        ["modificar_limites_jornada"] =
            ["jornada_id", "horas_semanales", "tope_diario", "minimo_diario", "dias_descanso_por_semana"],
        ["asignar_jornada_predeterminada"] = ["jornada_id"],
        ["asignar_jornada_a_plantilla_semanal"] = ["plantilla_id", "jornada_id"],
        ["quitar_jornada_de_plantilla_semanal"] = ["plantilla_id"],
        ["asignar_jornada"] = ["jornada_id", "identificaciones"],
        ["asignar_jornada_por_grupo"] = ["jornada_id"],
        ["desactivar_jornada"] = ["jornada_id"],
        ["modificar_ventana_de_entrada"] = ["turno_id", "amplitud"],
        ["crear_turno_de_horario_abierto"] = ["nombre", "duracion"],
        ["asignar_etiqueta"] = ["categoria", "valor", "identificaciones"],
        ["retirar_etiqueta"] = ["categoria", "identificaciones"]
    };

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ServidorMcp_MaterializaLasToolsEsperadas_CuandoSeListanLasTools()
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

            var esEscritura = nombre is "registrar_sede" or "registrar_colaborador" or "registrar_colaboradores" or "crear_turno" or "retirar_turno"
                or "agregar_franja" or "quitar_franja" or "agregar_subfranja" or "quitar_subfranja"
                or "asignar_sede_franja" or "crear_plantilla_semanal" or "retirar_plantilla_semanal"
                or "asignar_turno_a_dia" or "quitar_turno_de_dia" or "solicitar_programacion_turno"
                or "solicitar_programacion_turno_por_grupo" or "aplicar_plantilla_semanal"
                or "aplicar_plantilla_semanal_por_grupo"
                or "programar_ausencia" or "cancelar_ausencia"
                or "crear_jornada" or "modificar_limites_jornada" or "asignar_jornada_predeterminada"
                or "asignar_jornada_a_plantilla_semanal" or "quitar_jornada_de_plantilla_semanal"
                or "asignar_jornada" or "asignar_jornada_por_grupo" or "desactivar_jornada"
                or "modificar_ventana_de_entrada" or "crear_turno_de_horario_abierto"
                or "asignar_etiqueta" or "retirar_etiqueta";
            (meta!["readOnlyHint"]?.GetValue<bool>()).Should().Be(!esEscritura, $"{nombre} debe publicar readOnlyHint");
        }
    }
}
