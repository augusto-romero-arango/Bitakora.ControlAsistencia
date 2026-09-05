using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.AgregarFranja;
using Bitakora.ControlAsistencia.Mcp.Comandos.AgregarSubFranja;
using Bitakora.ControlAsistencia.Mcp.Comandos.AsignarSedeFranja;
using Bitakora.ControlAsistencia.Mcp.Comandos.CrearTurno;
using Bitakora.ControlAsistencia.Mcp.Comandos.QuitarFranja;
using Bitakora.ControlAsistencia.Mcp.Comandos.QuitarSubFranja;
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
            CrearTurnoTool.NombreTool, RetirarTurnoTool.NombreTool, AgregarFranjaTool.NombreTool, QuitarFranjaTool.NombreTool,
            AgregarSubFranjaTool.NombreTool, QuitarSubFranjaTool.NombreTool, AsignarSedeFranjaTool.NombreTool);
    }

    [Fact]
    public void ServidorMcp_DeclaraCadaToolComoFunction_CuandoSeInspeccionaElEnsamblado()
    {
        foreach (var metodo in MetodosDeTool)
            metodo.GetCustomAttribute<FunctionAttribute>().Should().NotBeNull(
                $"{metodo.DeclaringType!.Name}.{metodo.Name} debe ser una Function para que el host la registre");
    }

    // idempotentHint se omite a proposito: repetir el mismo codigo no es idempotente, da 409.
    // readOnlyHint es false en toda tool de este ensamblado: es el servidor Mcp.Comandos, ninguna
    // es de solo lectura (MEF-ADR-0047 decision 2).
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

    // destructiveHint es true en las tools que remueven algo del catalogo (retirar_turno,
    // quitar_franja, quitar_subfranja -- CA-5 de #609, CA-4 de #610); el resto solo crea o
    // agrega, nunca destruye.
    [Fact]
    public void ServidorMcp_DeclaraDestructiveHintEnLasToolsQueRemueven_CuandoSeInspeccionaElEnsamblado()
    {
        var destructivas = new HashSet<string>
        {
            RetirarTurnoTool.NombreTool, QuitarFranjaTool.NombreTool, QuitarSubFranjaTool.NombreTool
        };

        foreach (var metodo in MetodosDeTool)
        {
            var toolName = ParametroTrigger(metodo)!.GetCustomAttribute<McpToolTriggerAttribute>()!.ToolName;
            var metadata = ParametroTrigger(metodo)!.GetCustomAttribute<McpMetadataAttribute>()!;
            var esperado = destructivas.Contains(toolName);

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

    [Fact]
    public void AgregarFranja_DeclaraTurnoInicioFinComoRequeridosYCodigoSedeComoOpcional_CuandoSeInspeccionaLaTool()
    {
        var metodo = MetodosDeTool.Single(m =>
            ParametroTrigger(m)!.GetCustomAttribute<McpToolTriggerAttribute>()!.ToolName == AgregarFranjaTool.NombreTool);

        var propiedades = metodo.GetParameters()
            .Select(p => p.GetCustomAttribute<McpToolPropertyAttribute>())
            .Where(a => a is not null)
            .Select(a => (a!.PropertyName, a.IsRequired));

        propiedades.Should().Equal(
            ("turno", true),
            ("inicio", true),
            ("fin", true),
            ("codigo_sede", false));
    }

    [Fact]
    public void QuitarFranja_DeclaraTurnoYFranjaComoRequeridos_CuandoSeInspeccionaLaTool()
    {
        var metodo = MetodosDeTool.Single(m =>
            ParametroTrigger(m)!.GetCustomAttribute<McpToolTriggerAttribute>()!.ToolName == QuitarFranjaTool.NombreTool);

        var propiedades = metodo.GetParameters()
            .Select(p => p.GetCustomAttribute<McpToolPropertyAttribute>())
            .Where(a => a is not null)
            .Select(a => (a!.PropertyName, a.IsRequired));

        propiedades.Should().Equal(("turno", true), ("franja", true));
    }

    [Fact]
    public void AgregarSubFranja_DeclaraLosCincoParametrosComoRequeridos_CuandoSeInspeccionaLaTool()
    {
        var metodo = MetodosDeTool.Single(m =>
            ParametroTrigger(m)!.GetCustomAttribute<McpToolTriggerAttribute>()!.ToolName
                == AgregarSubFranjaTool.NombreTool);

        var propiedades = metodo.GetParameters()
            .Select(p => p.GetCustomAttribute<McpToolPropertyAttribute>())
            .Where(a => a is not null)
            .Select(a => (a!.PropertyName, a.IsRequired));

        propiedades.Should().Equal(
            ("turno", true), ("franja", true), ("tipo", true), ("inicio", true), ("fin", true));
    }

    [Fact]
    public void QuitarSubFranja_DeclaraLosCuatroParametrosComoRequeridos_CuandoSeInspeccionaLaTool()
    {
        var metodo = MetodosDeTool.Single(m =>
            ParametroTrigger(m)!.GetCustomAttribute<McpToolTriggerAttribute>()!.ToolName
                == QuitarSubFranjaTool.NombreTool);

        var propiedades = metodo.GetParameters()
            .Select(p => p.GetCustomAttribute<McpToolPropertyAttribute>())
            .Where(a => a is not null)
            .Select(a => (a!.PropertyName, a.IsRequired));

        propiedades.Should().Equal(("turno", true), ("franja", true), ("tipo", true), ("inicio", true));
    }

    [Fact]
    public void AsignarSedeFranja_DeclaraTurnoYFranjaComoRequeridosYCodigoSedeComoOpcional_CuandoSeInspeccionaLaTool()
    {
        var metodo = MetodosDeTool.Single(m =>
            ParametroTrigger(m)!.GetCustomAttribute<McpToolTriggerAttribute>()!.ToolName
                == AsignarSedeFranjaTool.NombreTool);

        var propiedades = metodo.GetParameters()
            .Select(p => p.GetCustomAttribute<McpToolPropertyAttribute>())
            .Where(a => a is not null)
            .Select(a => (a!.PropertyName, a.IsRequired));

        propiedades.Should().Equal(("turno", true), ("franja", true), ("codigo_sede", false));
    }
}
