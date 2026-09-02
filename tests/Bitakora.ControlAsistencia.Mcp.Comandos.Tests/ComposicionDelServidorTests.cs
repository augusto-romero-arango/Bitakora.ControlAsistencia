using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.Ejemplo;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests;

public class ComposicionDelServidorTests
{
    private static readonly IReadOnlyList<MethodInfo> MetodosDeTool =
        [.. typeof(EjemploListarTool).Assembly
            .GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Where(m => ParametroTrigger(m) is not null)];

    private static ParameterInfo? ParametroTrigger(MethodInfo metodo) =>
        metodo.GetParameters()
            .FirstOrDefault(p => p.GetCustomAttribute<McpToolTriggerAttribute>() is not null);

    [Fact]
    public void ServidorMcp_ExponeLaToolDeEjemplo_CuandoSeInspeccionaElEnsamblado()
    {
        var nombres = MetodosDeTool
            .Select(m => ParametroTrigger(m)!.GetCustomAttribute<McpToolTriggerAttribute>()!.ToolName);

        nombres.Should().ContainSingle().Which.Should().Be(EjemploListarTool.NombreTool);
    }

    [Fact]
    public void ServidorMcp_DeclaraCadaToolComoFunction_CuandoSeInspeccionaElEnsamblado()
    {
        foreach (var metodo in MetodosDeTool)
            metodo.GetCustomAttribute<FunctionAttribute>().Should().NotBeNull(
                $"{metodo.DeclaringType!.Name}.{metodo.Name} debe ser una Function para que el host la registre");
    }

    [Fact]
    public void ServidorMcp_DeclaraReadOnlyHintEnCadaTool_CuandoSeInspeccionaElEnsamblado()
    {
        foreach (var metodo in MetodosDeTool)
        {
            var metadata = ParametroTrigger(metodo)!.GetCustomAttribute<McpMetadataAttribute>();

            metadata.Should().NotBeNull(
                $"la tool de {metodo.DeclaringType!.Name} debe declarar su hint de solo lectura");
            metadata!.Json.Should().Contain("\"readOnlyHint\": true");
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
    public void EjemploListar_DeclaraFiltroNombreComoOpcional_CuandoSeInspeccionaLaTool()
    {
        var metodo = MetodosDeTool.Single(m =>
            ParametroTrigger(m)!.GetCustomAttribute<McpToolTriggerAttribute>()!.ToolName == EjemploListarTool.NombreTool);

        var propiedades = metodo.GetParameters()
            .Select(p => p.GetCustomAttribute<McpToolPropertyAttribute>())
            .Where(a => a is not null)
            .Select(a => (a!.PropertyName, a.IsRequired));

        propiedades.Should().ContainSingle().Which.Should().Be(("filtro_nombre", false));
    }
}
