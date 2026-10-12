using AwesomeAssertions;
using Bitakora.ControlAsistencia.Projections.Infraestructura;
using Bitakora.ControlAsistencia.ReadModels;
using Microsoft.Extensions.DependencyInjection;
using Marten;

namespace Bitakora.ControlAsistencia.Projections.Tests;

public class DocumentosMarcadosPorProyeccionTests
{
    private const string ConnectionStringDummy = "Host=localhost;Database=dummy";

    private static IEnumerable<Type> TiposPublicados(IDocumentStore store) =>
        store.Options.Events.Projections().SelectMany(fuente =>
        {
            var metodo = fuente.GetType().GetInterfaces()
                .SelectMany(i => i.GetMethods())
                .First(m => m.Name == "PublishedTypes");
            return (IEnumerable<Type>)metodo.Invoke(fuente, null)!;
        });

    private static IReadOnlyCollection<Type> TiposMarcados(string dominio) =>
        typeof(MaterializadoPorProyeccionAttribute).Assembly.GetTypes()
            .Where(t => t.Namespace == $"Bitakora.ControlAsistencia.ReadModels.{dominio}"
                && t.IsDefined(typeof(MaterializadoPorProyeccionAttribute), inherit: false))
            .ToList();

    private static void AssertParidad(IDocumentStore store, string dominio)
    {
        var publicados = TiposPublicados(store).Distinct().Select(t => t.FullName).Order().ToList();
        var marcados = TiposMarcados(dominio).Select(t => t.FullName).Order().ToList();

        marcados.Should().NotBeEmpty();
        publicados.Should().NotBeEmpty();
        marcados.Should().Equal(publicados,
            "los documentos que materializan las proyecciones del worker de {0} deben llevar [MaterializadoPorProyeccion] y viceversa",
            dominio);
    }

    [Fact]
    public void ConfigurarControlHoras_MarcaExactamenteLosDocumentosQueMaterializanSusProyecciones()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigurarControlHoras(ConnectionStringDummy);
        using var provider = services.BuildServiceProvider();

        AssertParidad(provider.GetRequiredService<IControlHorasProjectionStore>(), "ControlHoras");
    }

    [Fact]
    public void ConfigurarColaboradores_MarcaExactamenteLosDocumentosQueMaterializanSusProyecciones()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigurarColaboradores(ConnectionStringDummy);
        using var provider = services.BuildServiceProvider();

        AssertParidad(provider.GetRequiredService<IColaboradoresProjectionStore>(), "Colaboradores");
    }

    [Fact]
    public void ConfigurarProgramacion_MarcaExactamenteLosDocumentosQueMaterializanSusProyecciones()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigurarProgramacion(ConnectionStringDummy);
        using var provider = services.BuildServiceProvider();

        AssertParidad(provider.GetRequiredService<IProgramacionProjectionStore>(), "Programacion");
    }

    [Fact]
    public void ConfigurarSedes_MarcaExactamenteLosDocumentosQueMaterializanSusProyecciones()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigurarSedes(ConnectionStringDummy);
        using var provider = services.BuildServiceProvider();

        AssertParidad(provider.GetRequiredService<ISedesProjectionStore>(), "Sedes");
    }
}
