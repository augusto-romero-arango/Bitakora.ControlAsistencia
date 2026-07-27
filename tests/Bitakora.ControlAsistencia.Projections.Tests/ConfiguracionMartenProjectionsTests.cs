using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Bitakora.ControlAsistencia.Projections.Tests;

public class ConfiguracionMartenProjectionsTests
{
    private const string ConnectionStringDummy = "Host=localhost;Database=dummy";

    // Cada dominio que projection-test-writer (issue #365) cubra agrega aqui su propia llamada
    // directa -- ConfiguracionMartenProjections{Dominio}.Configurar{Dominio}(services,
    // ConnectionStringDummy) -- antes de construir el provider (ver config-test.md). Nunca a
    // traves de ConfigurarEventos: ese seam es wiring puro para Program.cs, no la superficie que
    // este test ejercita.
    [Fact]
    public void ServiceCollection_DebeConstruirElServiceProvider_SinNingunDominioRegistradoTodavia()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        using var provider = services.BuildServiceProvider();

        provider.Should().NotBeNull();
    }
}
