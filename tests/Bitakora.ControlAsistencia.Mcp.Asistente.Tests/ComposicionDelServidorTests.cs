using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.ListarSedes;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests;

public class ComposicionDelServidorTests
{
    private static readonly IReadOnlyList<MethodInfo> MetodosDeTool =
        [.. typeof(ListarSedesTool).Assembly
            .GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Where(m => ParametroTrigger(m) is not null)];

    private static ParameterInfo? ParametroTrigger(MethodInfo metodo) =>
        metodo.GetParameters()
            .FirstOrDefault(p => p.GetCustomAttribute<McpToolTriggerAttribute>() is not null);

    private static MethodInfo MetodoDe(string nombreTool) =>
        MetodosDeTool.Single(m =>
            ParametroTrigger(m)!.GetCustomAttribute<McpToolTriggerAttribute>()!.ToolName == nombreTool);

    private static IEnumerable<(string Nombre, bool Requerido)> PropiedadesDe(string nombreTool) =>
        MetodoDe(nombreTool).GetParameters()
            .Select(p => p.GetCustomAttribute<McpToolPropertyAttribute>())
            .Where(a => a is not null)
            .Select(a => (a!.PropertyName, a.IsRequired));

    [Fact]
    public void ServidorMcp_ExponeLasVeintiochoTools_CuandoSeInspeccionaElEnsamblado()
    {
        var nombres = MetodosDeTool
            .Select(m => ParametroTrigger(m)!.GetCustomAttribute<McpToolTriggerAttribute>()!.ToolName);

        nombres.Should().BeEquivalentTo(
            [
                "obtener_sesion", "cerrar_sesion", "listar_sedes", "listar_colaboradores",
                "buscar_colaboradores", "registrar_sede", "registrar_colaborador",
                "listar_turnos", "obtener_turno", "crear_turno", "retirar_turno", "agregar_franja",
                "quitar_franja", "agregar_subfranja", "quitar_subfranja", "asignar_sede_franja",
                "listar_plantillas_semanales", "obtener_plantilla_semanal", "crear_plantilla_semanal",
                "retirar_plantilla_semanal", "asignar_turno_a_dia", "quitar_turno_de_dia",
                "consultar_programacion", "consultar_ausencias", "solicitar_programacion_turno",
                "solicitar_programacion_turno_por_grupo", "programar_ausencia", "cancelar_ausencia"
            ]);
    }

    [Fact]
    public void ServidorMcp_NoExponeCerrarSesionDosVeces_CuandoSeInspeccionaElEnsamblado()
    {
        MetodosDeTool
            .Count(m => ParametroTrigger(m)!.GetCustomAttribute<McpToolTriggerAttribute>()!.ToolName == "cerrar_sesion")
            .Should().Be(1);
    }

    [Fact]
    public void ServidorMcp_DeclaraCadaToolComoFunction_CuandoSeInspeccionaElEnsamblado()
    {
        MetodosDeTool.Should().NotBeEmpty();
        foreach (var metodo in MetodosDeTool)
            metodo.GetCustomAttribute<FunctionAttribute>().Should().NotBeNull(
                $"{metodo.DeclaringType!.Name}.{metodo.Name} debe ser una Function para que el host la registre");
    }

    [Fact]
    public void ServidorMcp_DescribeTodasLasToolsYPropiedades_CuandoSeInspeccionaElEnsamblado()
    {
        MetodosDeTool.Should().NotBeEmpty();
        foreach (var metodo in MetodosDeTool)
        {
            ParametroTrigger(metodo)!.GetCustomAttribute<McpToolTriggerAttribute>()!
                .Description.Should().NotBeNullOrWhiteSpace();

            foreach (var propiedad in metodo.GetParameters()
                .Select(p => p.GetCustomAttribute<McpToolPropertyAttribute>())
                .Where(a => a is not null))
                propiedad!.Description.Should().NotBeNullOrWhiteSpace();
        }
    }

    private static readonly (string Nombre, string InicioDeDescripcion, string Hints)[] ContratoDeOrigen =
    [
        ("obtener_sesion", "Muestra con que sesion de autenticacion estas operando", "\"readOnlyHint\": true"),
        ("cerrar_sesion", "Te da el enlace para cerrar tu sesion de autenticacion (WorkOS)",
            "\"readOnlyHint\": true, \"destructiveHint\": false"),
        ("listar_sedes", "Lista las sedes activas de la empresa: codigo, nombre, ciudad y direccion.",
            "\"readOnlyHint\": true"),
        ("listar_colaboradores", "Lista los colaboradores vinculados: identificacion, codigo, nombre, sede y etiquetas",
            "\"readOnlyHint\": true"),
        ("buscar_colaboradores", "Busca colaboradores concretos por nombre o por identificacion",
            "\"readOnlyHint\": true"),
        ("registrar_sede", "Registra una sede (lugar de trabajo) nueva de la empresa.",
            "\"readOnlyHint\": false, \"destructiveHint\": false"),
        ("registrar_colaborador", "Pone a una persona bajo control de asistencia",
            "\"readOnlyHint\": false, \"destructiveHint\": false"),
        ("listar_turnos", "Lista el catalogo de turnos disponibles para programar: id, nombre y horario de cada uno.",
            "\"readOnlyHint\": true"),
        ("obtener_turno", "Obtiene el detalle de un turno del catalogo: sus franjas con horario, descansos, ",
            "\"readOnlyHint\": true"),
        ("crear_turno", "Crea un turno nuevo del catalogo. Por defecto nace vacio (turno incompleto)",
            "\"readOnlyHint\": false, \"destructiveHint\": false"),
        ("retirar_turno", "Retira un turno del catalogo por su nombre exacto",
            "\"readOnlyHint\": false, \"destructiveHint\": true"),
        ("agregar_franja", "Agrega una franja ordinaria (segmento continuo de trabajo) a un turno del catalogo",
            "\"readOnlyHint\": false, \"destructiveHint\": false"),
        ("quitar_franja", "Quita de un turno la franja ordinaria que empieza a la hora indicada (HH:mm)",
            "\"readOnlyHint\": false, \"destructiveHint\": true"),
        ("agregar_subfranja", "Agrega dentro de una franja ordinaria de un turno un descanso",
            "\"readOnlyHint\": false, \"destructiveHint\": false"),
        ("quitar_subfranja", "Quita de una franja ordinaria de un turno el descanso o extra que empieza a la hora",
            "\"readOnlyHint\": false, \"destructiveHint\": true"),
        ("asignar_sede_franja", "Asigna o cambia la sede prearmada de una franja ordinaria de un turno",
            "\"readOnlyHint\": false, \"destructiveHint\": false"),
        ("listar_plantillas_semanales", "Lista las plantillas semanales de turnos del catalogo: nombre, numero de semanas",
            "\"readOnlyHint\": true"),
        ("obtener_plantilla_semanal", "Devuelve el cuadro de una plantilla semanal por su nombre exacto",
            "\"readOnlyHint\": true"),
        ("crear_plantilla_semanal", "Crea una plantilla semanal de turnos: un molde de 1 a 6 semanas",
            "\"readOnlyHint\": false, \"destructiveHint\": false"),
        ("retirar_plantilla_semanal", "Retira una plantilla semanal del catalogo por su nombre exacto",
            "\"readOnlyHint\": false, \"destructiveHint\": true"),
        ("asignar_turno_a_dia", "Pone o reemplaza el turno de un dia de una plantilla semanal",
            "\"readOnlyHint\": false, \"destructiveHint\": false"),
        ("quitar_turno_de_dia", "Deja sin turno un dia de una plantilla semanal",
            "\"readOnlyHint\": false, \"destructiveHint\": true"),
        ("consultar_programacion", "Consulta que turno rige a cada colaborador en un rango de fechas",
            "\"readOnlyHint\": true"),
        ("consultar_ausencias", "Consulta quien falta en un periodo: por colaborador, sus ausencias",
            "\"readOnlyHint\": true"),
        ("solicitar_programacion_turno", "Programa un turno a una lista de colaboradores en una sede",
            "\"readOnlyHint\": false, \"destructiveHint\": false"),
        ("solicitar_programacion_turno_por_grupo", "Programa un turno a todos los colaboradores de un grupo",
            "\"readOnlyHint\": false, \"destructiveHint\": false"),
        ("programar_ausencia", "Registra una ausencia de un colaborador: dias completos en que no vendra a trabajar",
            "\"readOnlyHint\": false, \"destructiveHint\": false"),
        ("cancelar_ausencia", "Cancela ausencias de un colaborador en un periodo.",
            "\"readOnlyHint\": false, \"destructiveHint\": true")
    ];

    [Fact]
    public void ServidorMcp_ConservaLaDescripcionDeOrigen_CuandoSeInspeccionaCadaTool()
    {
        foreach (var (nombre, inicio, _) in ContratoDeOrigen)
            ParametroTrigger(MetodoDe(nombre))!.GetCustomAttribute<McpToolTriggerAttribute>()!
                .Description.Should().StartWith(inicio, $"la descripcion de {nombre} no cambia en el traslado");
    }

    [Fact]
    public void ServidorMcp_ConservaLosHintsDeOrigen_CuandoSeInspeccionaCadaTool()
    {
        foreach (var (nombre, _, hints) in ContratoDeOrigen)
        {
            var metadata = ParametroTrigger(MetodoDe(nombre))!.GetCustomAttribute<McpMetadataAttribute>();

            metadata.Should().NotBeNull($"{nombre} debe declarar sus hints");
            metadata!.Json.Should().Contain(hints);
        }
    }

    [Fact]
    public void ObtenerSesionYCerrarSesion_NoDeclaranParametros_CuandoSeInspeccionanLasTools()
    {
        PropiedadesDe("obtener_sesion").Should().BeEmpty();
        PropiedadesDe("cerrar_sesion").Should().BeEmpty();
    }

    [Fact]
    public void ListarSedes_DeclaraSusParametrosComoOpcionales_CuandoSeInspeccionaLaTool() =>
        PropiedadesDe("listar_sedes").Should().BeEquivalentTo([("filtro_nombre", false)]);

    [Fact]
    public void ListarColaboradores_DeclaraSusParametrosComoOpcionales_CuandoSeInspeccionaLaTool() =>
        PropiedadesDe("listar_colaboradores").Should().BeEquivalentTo(
            [("identificacion", false), ("sede", false), ("etiquetas", false), ("fecha_referencia", false)]);

    [Fact]
    public void BuscarColaboradores_DeclaraSusParametrosComoOpcionales_CuandoSeInspeccionaLaTool() =>
        PropiedadesDe("buscar_colaboradores").Should().BeEquivalentTo(
            [("nombre", false), ("identificaciones", false)]);

    [Fact]
    public void RegistrarSede_DeclaraCodigoYNombreComoObligatorios_CuandoSeInspeccionaLaTool() =>
        PropiedadesDe("registrar_sede").Should().BeEquivalentTo(
            [("codigo", true), ("nombre", true), ("ciudad", false), ("direccion", false)]);

    [Fact]
    public void RegistrarColaborador_DeclaraSusObligatoriosYOpcionales_CuandoSeInspeccionaLaTool() =>
        PropiedadesDe("registrar_colaborador").Should().BeEquivalentTo(
            [
                ("tipo_identificacion", true), ("numero_identificacion", true), ("primer_nombre", true),
                ("segundo_nombre", false), ("primer_apellido", true), ("segundo_apellido", false),
                ("codigo_colaborador", true), ("fecha_inicio", true), ("codigo_sede", false)
            ]);

    [Fact]
    public void ListarTurnos_DeclaraElFiltroComoOpcional_CuandoSeInspeccionaLaTool() =>
        PropiedadesDe("listar_turnos").Should().BeEquivalentTo([("filtro_nombre", false)]);

    [Fact]
    public void ObtenerTurno_DeclaraElIdComoObligatorio_CuandoSeInspeccionaLaTool() =>
        PropiedadesDe("obtener_turno").Should().BeEquivalentTo([("id", true)]);

    [Fact]
    public void CrearTurno_DeclaraElNombreComoObligatorio_CuandoSeInspeccionaLaTool() =>
        PropiedadesDe("crear_turno").Should().BeEquivalentTo([("nombre", true), ("es_descanso", false)]);

    [Fact]
    public void RetirarTurno_DeclaraElTurnoComoObligatorio_CuandoSeInspeccionaLaTool() =>
        PropiedadesDe("retirar_turno").Should().BeEquivalentTo([("turno", true)]);

    [Fact]
    public void AgregarFranja_DeclaraSusObligatoriosYOpcionales_CuandoSeInspeccionaLaTool() =>
        PropiedadesDe("agregar_franja").Should().BeEquivalentTo(
            [("turno", true), ("inicio", true), ("fin", true), ("codigo_sede", false)]);

    [Fact]
    public void QuitarFranja_DeclaraTurnoYFranjaComoObligatorios_CuandoSeInspeccionaLaTool() =>
        PropiedadesDe("quitar_franja").Should().BeEquivalentTo([("turno", true), ("franja", true)]);

    [Fact]
    public void AgregarSubFranja_DeclaraTodosSusParametrosComoObligatorios_CuandoSeInspeccionaLaTool() =>
        PropiedadesDe("agregar_subfranja").Should().BeEquivalentTo(
            [("turno", true), ("franja", true), ("tipo", true), ("inicio", true), ("fin", true)]);

    [Fact]
    public void QuitarSubFranja_DeclaraTodosSusParametrosComoObligatorios_CuandoSeInspeccionaLaTool() =>
        PropiedadesDe("quitar_subfranja").Should().BeEquivalentTo(
            [("turno", true), ("franja", true), ("tipo", true), ("inicio", true)]);

    [Fact]
    public void AsignarSedeFranja_DeclaraCodigoSedeComoOpcional_CuandoSeInspeccionaLaTool() =>
        PropiedadesDe("asignar_sede_franja").Should().BeEquivalentTo(
            [("turno", true), ("franja", true), ("codigo_sede", false)]);

    [Fact]
    public void ListarPlantillasSemanales_DeclaraElFiltroComoOpcional_CuandoSeInspeccionaLaTool() =>
        PropiedadesDe("listar_plantillas_semanales").Should().BeEquivalentTo([("filtro_nombre", false)]);

    [Fact]
    public void ObtenerPlantillaSemanal_DeclaraLaPlantillaComoObligatoria_CuandoSeInspeccionaLaTool() =>
        PropiedadesDe("obtener_plantilla_semanal").Should().BeEquivalentTo([("plantilla", true)]);

    [Fact]
    public void CrearPlantillaSemanal_DeclaraNombreYDiasComoObligatorios_CuandoSeInspeccionaLaTool() =>
        PropiedadesDe("crear_plantilla_semanal").Should().BeEquivalentTo(
            [("nombre", true), ("semanas", false), ("dias", true)]);

    [Fact]
    public void RetirarPlantillaSemanal_DeclaraLaPlantillaComoObligatoria_CuandoSeInspeccionaLaTool() =>
        PropiedadesDe("retirar_plantilla_semanal").Should().BeEquivalentTo([("plantilla", true)]);

    [Fact]
    public void AsignarTurnoADia_DeclaraSusObligatoriosYOpcionales_CuandoSeInspeccionaLaTool() =>
        PropiedadesDe("asignar_turno_a_dia").Should().BeEquivalentTo(
            [("plantilla", true), ("turno", true), ("dia", true), ("semana", false)]);

    [Fact]
    public void QuitarTurnoDeDia_DeclaraSusObligatoriosYOpcionales_CuandoSeInspeccionaLaTool() =>
        PropiedadesDe("quitar_turno_de_dia").Should().BeEquivalentTo(
            [("plantilla", true), ("dia", true), ("semana", false)]);

    [Fact]
    public void ConsultarProgramacion_DeclaraDesdeYHastaComoObligatorios_CuandoSeInspeccionaLaTool() =>
        PropiedadesDe("consultar_programacion").Should().BeEquivalentTo(
            [("desde", true), ("hasta", true), ("codigo_colaborador", false), ("sede_id", false)]);

    [Fact]
    public void ConsultarAusencias_DeclaraDesdeYHastaComoObligatorios_CuandoSeInspeccionaLaTool() =>
        PropiedadesDe("consultar_ausencias").Should().BeEquivalentTo(
            [("desde", true), ("hasta", true), ("codigos_colaborador", false)]);

    [Fact]
    public void SolicitarProgramacionTurno_DeclaraLaSedeDeProgramacionComoOpcional_CuandoSeInspeccionaLaTool() =>
        PropiedadesDe("solicitar_programacion_turno").Should().BeEquivalentTo(
            [("desde", true), ("hasta", true), ("turno", true), ("sede_de_programacion", false), ("identificaciones", true)]);

    [Fact]
    public void ProgramarAusencia_DeclaraTodosSusParametrosComoObligatorios_CuandoSeInspeccionaLaTool() =>
        PropiedadesDe("programar_ausencia").Should().BeEquivalentTo(
            [("identificacion", true), ("desde", true), ("hasta", true), ("motivo", true)]);

    [Fact]
    public void CancelarAusencia_DeclaraCompletaComoOpcional_CuandoSeInspeccionaLaTool() =>
        PropiedadesDe("cancelar_ausencia").Should().BeEquivalentTo(
            [("identificacion", true), ("desde", true), ("hasta", true), ("completa", false)]);

    [Fact]
    public void SolicitarProgramacionTurnoPorGrupo_DeclaraLaVentanaYElTurnoObligatoriosYLaSedeDeProgramacionOpcional_CuandoSeInspeccionaLaTool() =>
        PropiedadesDe("solicitar_programacion_turno_por_grupo").Should().BeEquivalentTo(
            [("desde", true), ("hasta", true), ("turno", true), ("sede_de_programacion", false),
             ("sede", false), ("etiquetas", false)]);

    [Fact]
    public void SolicitarProgramacionTurno_RemiteALaToolDeGrupo_CuandoSeInspeccionaSuDescripcion() =>
        ParametroTrigger(MetodoDe("solicitar_programacion_turno"))!
            .GetCustomAttribute<McpToolTriggerAttribute>()!
            .Description.Should().Contain("solicitar_programacion_turno_por_grupo");

    [Fact]
    public void HostJson_MencionaLaToolDeGrupo_CuandoSeLeenLasInstructions()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null
            && !File.Exists(Path.Combine(directorio.FullName, "src", "Bitakora.ControlAsistencia.Mcp.Asistente", "host.json")))
            directorio = directorio.Parent;

        directorio.Should().NotBeNull("host.json del servidor debe ser localizable desde el repo");
        File.ReadAllText(Path.Combine(directorio!.FullName, "src", "Bitakora.ControlAsistencia.Mcp.Asistente", "host.json"))
            .Should().Contain("solicitar_programacion_turno_por_grupo");
    }

    [Fact]
    public void SolicitarProgramacionTurno_DescribeLaSedeDeProgramacionComoOpcionalYSugerida_CuandoSeInspeccionanLasDescripciones() =>
        AsegurarSedeDeProgramacionOpcional("solicitar_programacion_turno");

    [Fact]
    public void SolicitarProgramacionTurnoPorGrupo_DescribeLaSedeDeProgramacionComoOpcionalYSugerida_CuandoSeInspeccionanLasDescripciones() =>
        AsegurarSedeDeProgramacionOpcional("solicitar_programacion_turno_por_grupo");

    private static void AsegurarSedeDeProgramacionOpcional(string nombreTool)
    {
        var metodo = MetodoDe(nombreTool);
        var descripcionTool = ParametroTrigger(metodo)!.GetCustomAttribute<McpToolTriggerAttribute>()!.Description;
        var descripcionParametro = metodo.GetParameters()
            .Select(p => p.GetCustomAttribute<McpToolPropertyAttribute>())
            .Single(a => a?.PropertyName == "sede_de_programacion")!.Description;

        foreach (var texto in new[] { descripcionTool, descripcionParametro })
        {
            texto.Should().NotContain("nunca la asumas");
            texto.Should().NotContain("pidesela siempre");
            texto.ToLowerInvariant().Should().Contain("opcional");
        }
        descripcionTool.ToLowerInvariant().Should().Contain("sede de cada colaborador");
        descripcionTool.ToLowerInvariant().Should().Contain("sede de trabajo");
    }

    [Fact]
    public void HostJson_NoOrdenaPedirLaSedeDeProgramacionSiempre_CuandoSeLeenLasInstructions()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null
            && !File.Exists(Path.Combine(directorio.FullName, "src", "Bitakora.ControlAsistencia.Mcp.Asistente", "host.json")))
            directorio = directorio.Parent;

        directorio.Should().NotBeNull("host.json del servidor debe ser localizable desde el repo");
        var instructions = File.ReadAllText(
            Path.Combine(directorio!.FullName, "src", "Bitakora.ControlAsistencia.Mcp.Asistente", "host.json"));
        instructions.Should().NotContain("pidesela siempre al usuario");
        instructions.Should().Contain("opcional");
    }
}
