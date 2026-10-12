using AwesomeAssertions;
using Bitakora.ControlAsistencia.Projections.Infraestructura;
using Bitakora.ControlAsistencia.ReadModels;
using JasperFx.Events.Projections;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace Bitakora.ControlAsistencia.Projections.Tests;

public class DocumentosMarcadosPorProyeccionTests
{
    private const string ConnectionStringDummy = "Host=localhost;Database=dummy";
    private const string NamespaceReadModels = "Bitakora.ControlAsistencia.ReadModels";

    private static readonly string[] DominiosConStore = ["ControlHoras", "Colaboradores", "Programacion", "Sedes"];

    private static IEnumerable<Type> TiposMarcados() =>
        typeof(MaterializadoPorProyeccionAttribute).Assembly.GetTypes()
            .Where(t => t.IsDefined(typeof(MaterializadoPorProyeccionAttribute), inherit: false));

    // ISubscriptionSource no expone PublishedTypes(): hace falta el cast a IProjectionSource.
    private static IEnumerable<Type> TiposPublicados(IDocumentStore store) =>
        store.Options.Events.Projections()
            .Cast<IProjectionSource<IDocumentOperations, IQuerySession>>()
            .SelectMany(fuente => fuente.PublishedTypes());

    private static void AssertParidad<TStore>(Action<IServiceCollection> configurarDominio, string dominio)
        where TStore : IDocumentStore
    {
        var services = new ServiceCollection();
        services.AddLogging();
        configurarDominio(services);
        using var provider = services.BuildServiceProvider();

        var publicados = TiposPublicados(provider.GetRequiredService<TStore>())
            .Select(t => t.FullName).Distinct().Order().ToList();
        var marcados = TiposMarcados()
            .Where(t => t.Namespace == $"{NamespaceReadModels}.{dominio}")
            .Select(t => t.FullName).Order().ToList();

        marcados.Should().NotBeEmpty();
        publicados.Should().NotBeEmpty();
        marcados.Should().Equal(publicados,
            "los documentos que materializan las proyecciones del worker de {0} deben llevar [MaterializadoPorProyeccion] y viceversa",
            dominio);
    }

    [Fact]
    public void ConfigurarControlHoras_MarcaExactamenteLosDocumentosQueMaterializanSusProyecciones() =>
        AssertParidad<IControlHorasProjectionStore>(
            services => services.ConfigurarControlHoras(ConnectionStringDummy), "ControlHoras");

    [Fact]
    public void ConfigurarColaboradores_MarcaExactamenteLosDocumentosQueMaterializanSusProyecciones() =>
        AssertParidad<IColaboradoresProjectionStore>(
            services => services.ConfigurarColaboradores(ConnectionStringDummy), "Colaboradores");

    [Fact]
    public void ConfigurarProgramacion_MarcaExactamenteLosDocumentosQueMaterializanSusProyecciones() =>
        AssertParidad<IProgramacionProjectionStore>(
            services => services.ConfigurarProgramacion(ConnectionStringDummy), "Programacion");

    [Fact]
    public void ConfigurarSedes_MarcaExactamenteLosDocumentosQueMaterializanSusProyecciones() =>
        AssertParidad<ISedesProjectionStore>(
            services => services.ConfigurarSedes(ConnectionStringDummy), "Sedes");

    // Un tipo marcado fuera de estos namespaces escaparia a las cuatro paridades de arriba: un
    // dominio nuevo con store en el worker necesita aqui su entrada y su propio test de paridad.
    [Fact]
    public void MaterializadoPorProyeccion_SoloMarcaDocumentosDeDominiosConParidadVerificada()
    {
        var namespacesCubiertos = DominiosConStore.Select(dominio => $"{NamespaceReadModels}.{dominio}");

        TiposMarcados().Select(t => t.Namespace).Distinct().Should().BeSubsetOf(namespacesCubiertos);
    }
}
