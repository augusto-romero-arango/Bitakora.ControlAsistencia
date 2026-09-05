using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.CrearTurno;
using Bitakora.ControlAsistencia.Mcp.Comandos.RegistrarColaborador;
using Bitakora.ControlAsistencia.Mcp.Comandos.RegistrarSede;
using Bitakora.ControlAsistencia.Mcp.Comandos.RetirarTurno;
using Bitakora.ControlAsistencia.Mcp.Comandos.SolicitarProgramacionTurno;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests;

public class ComposicionDelServidorTests
{
    private static readonly IReadOnlyList<MethodInfo> MetodosDeTool =
        [.. typeof(RegistrarSedeTool).Assembly
            .GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Where(m => ParametroTrigger(m) is not null)];

    private static ParameterInfo? ParametroTrigger(MethodInfo metodo) =>
        metodo.GetParameters()
            .FirstOrDefault(p => p.GetCustomAttribute<McpToolTriggerAttribute>() is not null);

    [Fact]
    public void ServidorMcp_ExponeElCatalogoDeTools_CuandoSeInspeccionaElEnsamblado()
    {
        var nombres = MetodosDeTool
            .Select(m => ParametroTrigger(m)!.GetCustomAttribute<McpToolTriggerAttribute>()!.ToolName);

        nombres.Should().BeEquivalentTo(
            RegistrarSedeTool.NombreTool, RegistrarColaboradorTool.NombreTool, SolicitarProgramacionTurnoTool.NombreTool,
            CrearTurnoTool.NombreTool, RetirarTurnoTool.NombreTool);
    }

    [Fact]
    public void ServidorMcp_DeclaraCadaToolComoFunction_CuandoSeInspeccionaElEnsamblado()
    {
        foreach (var metodo in MetodosDeTool)
            metodo.GetCustomAttribute<FunctionAttribute>().Should().NotBeNull(
                $"{metodo.DeclaringType!.Name}.{metodo.Name} debe ser una Function para que el host la registre");
    }

    // idempotentHint se omite a proposito: repetir el mismo codigo no es idempotente, da 409.
    // readOnlyHint es false en las 5 tools -- ninguna es de solo lectura, todas escriben en el
    // dominio (este es el servidor Mcp.Comandos, MEF-ADR-0047 decision 2).
    [Fact]
    public void ServidorMcp_DeclaraReadOnlyHintFalseEnCadaTool_CuandoSeInspeccionaElEnsamblado()
    {
        foreach (var metodo in MetodosDeTool)
        {
            var metadata = ParametroTrigger(metodo)!.GetCustomAttribute<McpMetadataAttribute>();

            metadata.Should().NotBeNull(
                $"la tool de {metodo.DeclaringType!.Name} debe declarar sus hints de escritura");
            metadata!.Json.Should().Contain("\"readOnlyHint\": false");
        }
    }

    // CA-5: destructiveHint es true unicamente en retirar_turno (saca un turno del catalogo); el
    // resto de las tools solo crea o agrega, nunca destruye.
    [Fact]
    public void ServidorMcp_DeclaraDestructiveHintSoloEnRetirarTurno_CuandoSeInspeccionaElEnsamblado()
    {
        foreach (var metodo in MetodosDeTool)
        {
            var toolName = ParametroTrigger(metodo)!.GetCustomAttribute<McpToolTriggerAttribute>()!.ToolName;
            var metadata = ParametroTrigger(metodo)!.GetCustomAttribute<McpMetadataAttribute>()!;
            var esperado = toolName == RetirarTurnoTool.NombreTool;

            metadata.Json.Should().Contain(
                esperado ? "\"destructiveHint\": true" : "\"destructiveHint\": false",
                $"{toolName} debe declarar destructiveHint={esperado}");
        }
    }

    [Fact]
    public void ServidorMcp_DescribeTodasLasToolsYPropiedades_CuandoSeInspeccionaElEnsamblado()
    {
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

    [Fact]
    public void RegistrarSede_DeclaraCodigoYNombreComoRequeridosYCiudadDireccionComoOpcionales_CuandoSeInspeccionaLaTool()
    {
        var metodo = MetodosDeTool.Single(m =>
            ParametroTrigger(m)!.GetCustomAttribute<McpToolTriggerAttribute>()!.ToolName == RegistrarSedeTool.NombreTool);

        var propiedades = metodo.GetParameters()
            .Select(p => p.GetCustomAttribute<McpToolPropertyAttribute>())
            .Where(a => a is not null)
            .Select(a => (a!.PropertyName, a.IsRequired));

        propiedades.Should().Equal(
            ("codigo", true),
            ("nombre", true),
            ("ciudad", false),
            ("direccion", false));
    }

    [Fact]
    public void RegistrarColaborador_DeclaraSeisRequeridosYTresOpcionales_CuandoSeInspeccionaLaTool()
    {
        var metodo = MetodosDeTool.Single(m =>
            ParametroTrigger(m)!.GetCustomAttribute<McpToolTriggerAttribute>()!.ToolName
                == RegistrarColaboradorTool.NombreTool);

        var propiedades = metodo.GetParameters()
            .Select(p => p.GetCustomAttribute<McpToolPropertyAttribute>())
            .Where(a => a is not null)
            .Select(a => (a!.PropertyName, a.IsRequired));

        propiedades.Should().Equal(
            ("tipo_identificacion", true),
            ("numero_identificacion", true),
            ("primer_nombre", true),
            ("segundo_nombre", false),
            ("primer_apellido", true),
            ("segundo_apellido", false),
            ("codigo_colaborador", true),
            ("fecha_inicio", true),
            ("codigo_sede", false));
    }

    [Fact]
    public void SolicitarProgramacionTurno_DeclaraLosCincoParametrosComoRequeridos_CuandoSeInspeccionaLaTool()
    {
        var metodo = MetodosDeTool.Single(m =>
            ParametroTrigger(m)!.GetCustomAttribute<McpToolTriggerAttribute>()!.ToolName
                == SolicitarProgramacionTurnoTool.NombreTool);

        var propiedades = metodo.GetParameters()
            .Select(p => p.GetCustomAttribute<McpToolPropertyAttribute>())
            .Where(a => a is not null)
            .Select(a => (a!.PropertyName, a.IsRequired));

        propiedades.Should().Equal(
            ("desde", true),
            ("hasta", true),
            ("turno", true),
            ("sede_de_programacion", true),
            ("identificaciones", true));
    }

    [Fact]
    public void CrearTurno_DeclaraNombreComoRequeridoYEsDescansoComoOpcional_CuandoSeInspeccionaLaTool()
    {
        var metodo = MetodosDeTool.Single(m =>
            ParametroTrigger(m)!.GetCustomAttribute<McpToolTriggerAttribute>()!.ToolName == CrearTurnoTool.NombreTool);

        var propiedades = metodo.GetParameters()
            .Select(p => p.GetCustomAttribute<McpToolPropertyAttribute>())
            .Where(a => a is not null)
            .Select(a => (a!.PropertyName, a.IsRequired));

        propiedades.Should().Equal(
            ("nombre", true),
            ("es_descanso", false));
    }

    [Fact]
    public void RetirarTurno_DeclaraTurnoComoRequerido_CuandoSeInspeccionaLaTool()
    {
        var metodo = MetodosDeTool.Single(m =>
            ParametroTrigger(m)!.GetCustomAttribute<McpToolTriggerAttribute>()!.ToolName == RetirarTurnoTool.NombreTool);

        var propiedades = metodo.GetParameters()
            .Select(p => p.GetCustomAttribute<McpToolPropertyAttribute>())
            .Where(a => a is not null)
            .Select(a => (a!.PropertyName, a.IsRequired));

        propiedades.Should().Equal(("turno", true));
    }
}
