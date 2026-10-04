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
    public void ServidorMcp_ExponeLasSieteToolsMigradas_CuandoSeInspeccionaElEnsamblado()
    {
        var nombres = MetodosDeTool
            .Select(m => ParametroTrigger(m)!.GetCustomAttribute<McpToolTriggerAttribute>()!.ToolName);

        nombres.Should().BeEquivalentTo(
            [
                "obtener_sesion", "cerrar_sesion", "listar_sedes", "listar_colaboradores",
                "buscar_colaboradores", "registrar_sede", "registrar_colaborador"
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
            "\"readOnlyHint\": false, \"destructiveHint\": false")
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
}
