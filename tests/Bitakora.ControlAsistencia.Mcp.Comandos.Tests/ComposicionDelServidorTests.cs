using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.RegistrarColaborador;
using Bitakora.ControlAsistencia.Mcp.Comandos.RegistrarSede;
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

        nombres.Should().BeEquivalentTo(RegistrarSedeTool.NombreTool, RegistrarColaboradorTool.NombreTool);
    }

    [Fact]
    public void ServidorMcp_DeclaraCadaToolComoFunction_CuandoSeInspeccionaElEnsamblado()
    {
        foreach (var metodo in MetodosDeTool)
            metodo.GetCustomAttribute<FunctionAttribute>().Should().NotBeNull(
                $"{metodo.DeclaringType!.Name}.{metodo.Name} debe ser una Function para que el host la registre");
    }

    // idempotentHint se omite a proposito: repetir el mismo codigo no es idempotente, da 409.
    [Fact]
    public void ServidorMcp_DeclaraHintsDeEscrituraEnCadaTool_CuandoSeInspeccionaElEnsamblado()
    {
        foreach (var metodo in MetodosDeTool)
        {
            var metadata = ParametroTrigger(metodo)!.GetCustomAttribute<McpMetadataAttribute>();

            metadata.Should().NotBeNull(
                $"la tool de {metodo.DeclaringType!.Name} debe declarar sus hints de escritura");
            metadata!.Json.Should().Contain("\"readOnlyHint\": false");
            metadata.Json.Should().Contain("\"destructiveHint\": false");
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
}
