using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.ListarTurnos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests;

// CA-6 (issue #502): intento del test de composicion del host MCP, documentado como insumo para
// harness#761.
//
// Resultado del intento: el analogo exacto de MEF-ADR-0029 (interrogar el registro de tools que
// sirve tools/list) NO es alcanzable en un unit test con la extension 1.6.0 -- ese registro
// (DefaultToolRegistry) vive en el paquete del HOST (Microsoft.Azure.Functions.Extensions.Mcp),
// que corre dentro del Functions host, no en el worker que este proyecto compila. Lo que si se
// puede fijar desde el worker es la metadata declarada: estos tests reflejan el ensamblado y
// pinnean el catalogo de tools (nombres, obligatoriedad de parametros, hint de solo lectura),
// que es lo que el host materializa en tools/list. La verificacion del registro vivo queda en la
// verificacion manual end-to-end del onboarding (#509 CA-4).
//
// Nota sobre readOnlyHint (CA-2): la extension 1.6.0 no soporta ToolAnnotations del spec MCP
// (verificado en el codigo fuente de Azure/azure-functions-mcp-extension: el registro solo emite
// name/description/schemas/_meta). El hint viaja en _meta via McpMetadata; la anotacion formal
// annotations.readOnlyHint quedara pendiente de que la extension la exponga.
public class ComposicionDelServidorTests
{
    private static readonly IReadOnlyList<MethodInfo> MetodosDeTool =
        [.. typeof(ListarTurnosTool).Assembly
            .GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Where(m => ParametroTrigger(m) is not null)];

    private static ParameterInfo? ParametroTrigger(MethodInfo metodo) =>
        metodo.GetParameters()
            .FirstOrDefault(p => p.GetCustomAttribute<McpToolTriggerAttribute>() is not null);

    [Fact]
    public void ServidorMcp_ExponeLasCuatroToolsDeConsulta_CuandoSeInspeccionaElEnsamblado()
    {
        var nombres = MetodosDeTool
            .Select(m => ParametroTrigger(m)!.GetCustomAttribute<McpToolTriggerAttribute>()!.ToolName);

        nombres.Should().BeEquivalentTo(
            "listar_turnos", "obtener_turno", "listar_sedes", "consultar_programacion");
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
            metadata.Json.Should().Contain("\"readOnlyHint\": true");
        }
    }

    [Fact]
    public void ServidorMcp_DescribeTodasLasToolsYPropiedadesEnEspanol_CuandoSeInspeccionaElEnsamblado()
    {
        foreach (var metodo in MetodosDeTool)
        {
            ParametroTrigger(metodo)!.GetCustomAttribute<McpToolTriggerAttribute>()!
                .Description.Should().NotBeNullOrWhiteSpace();

            foreach (var propiedad in PropiedadesConDescripcion(metodo))
                propiedad.Descripcion.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void ConsultarProgramacion_DeclaraElRangoObligatorioYLosFiltrosOpcionales_CuandoSeInspeccionaLaTool()
    {
        var metodo = MetodosDeTool.Single(m =>
            ParametroTrigger(m)!.GetCustomAttribute<McpToolTriggerAttribute>()!.ToolName
                == "consultar_programacion");

        Propiedades(metodo).Should().BeEquivalentTo(
        [
            ("desde", true),
            ("hasta", true),
            ("codigo_colaborador", false),
            ("sede_id", false)
        ], opciones => opciones.WithoutStrictOrdering(), "desde/hasta son obligatorios (CA-3)");
    }

    [Fact]
    public void ObtenerTurno_DeclaraElIdObligatorio_CuandoSeInspeccionaLaTool()
    {
        var metodo = MetodosDeTool.Single(m =>
            ParametroTrigger(m)!.GetCustomAttribute<McpToolTriggerAttribute>()!.ToolName
                == "obtener_turno");

        Propiedades(metodo).Should().ContainSingle().Which.Should().Be(("id", true));
    }

    private static List<(string Nombre, bool Obligatoria, string? Descripcion)> PropiedadesConDescripcion(
        MethodInfo metodo) =>
        [.. metodo.GetParameters()
            .Select(p => p.GetCustomAttribute<McpToolPropertyAttribute>())
            .Where(a => a is not null)
            .Select(a => (a!.PropertyName, a.IsRequired, a.Description))];

    private static List<(string Nombre, bool Obligatoria)> Propiedades(MethodInfo metodo) =>
        [.. PropiedadesConDescripcion(metodo).Select(p => (p.Nombre, p.Obligatoria))];
}
