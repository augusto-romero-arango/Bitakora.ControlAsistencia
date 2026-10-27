using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarAdvertenciasProgramacion;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Infraestructura;

public class ComposicionContenedorTests
{
    private static ServiceProvider ConstruirProveedor()
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Api:Programacion:BaseUrl"] = "https://programacion.test/",
                ["Api:Sedes:BaseUrl"] = "https://sedes.test/",
                ["Api:Colaboradores:BaseUrl"] = "https://colaboradores.test/",
                ["Api:ControlHoras:BaseUrl"] = "https://controlhoras.test/",
            })
            .Build();

        var services = new ServiceCollection();
        services.ConfigurarServiciosMcp(configuracion);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private static IEnumerable<Type> TiposDeTool() =>
        typeof(ResolutorCandidatosPorGrupo).Assembly
            .GetTypes()
            .Where(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Any(m => m.GetCustomAttribute<FunctionAttribute>() is not null
                    && m.GetParameters().Any(p => p.GetCustomAttribute<McpToolTriggerAttribute>() is not null)));

    [Fact]
    public void ConfigurarServiciosMcp_ActivaCadaTool_ConElContenedorReal()
    {
        using var proveedor = ConstruirProveedor();
        using var alcance = proveedor.CreateScope();
        var tipos = TiposDeTool().ToList();

        tipos.Should().NotBeEmpty();
        foreach (var tipo in tipos)
        {
            var activar = () => ActivatorUtilities.CreateInstance(alcance.ServiceProvider, tipo);
            activar.Should().NotThrow($"la tool {tipo.Name} debe poder activarse desde el contenedor");
        }
    }

    [Fact]
    public void ConfigurarServiciosMcp_ActivaConsultarAdvertenciasProgramacionTool()
    {
        using var proveedor = ConstruirProveedor();
        using var alcance = proveedor.CreateScope();

        var activar = () => ActivatorUtilities.CreateInstance<ConsultarAdvertenciasProgramacionTool>(alcance.ServiceProvider);

        activar.Should().NotThrow();
    }

    [Fact]
    public void ConfigurarServiciosMcp_RegistraResolutorCandidatosPorGrupoComoTransient()
    {
        using var proveedor = ConstruirProveedor();
        using var alcance = proveedor.CreateScope();

        var primero = alcance.ServiceProvider.GetRequiredService<ResolutorCandidatosPorGrupo>();
        var segundo = alcance.ServiceProvider.GetRequiredService<ResolutorCandidatosPorGrupo>();

        primero.Should().NotBeSameAs(segundo);
    }

    [Fact]
    public void ConfigurarServiciosMcp_RegistraResolutorPlantillaTurnosPorIdComoTransient()
    {
        using var proveedor = ConstruirProveedor();
        using var alcance = proveedor.CreateScope();

        var primero = alcance.ServiceProvider.GetRequiredService<ResolutorPlantillaTurnosPorId>();
        var segundo = alcance.ServiceProvider.GetRequiredService<ResolutorPlantillaTurnosPorId>();

        primero.Should().NotBeSameAs(segundo);
    }
}
