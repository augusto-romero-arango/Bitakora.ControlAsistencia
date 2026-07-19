// Issue #221: guardrail de CI que compone el contenedor DI real del dominio (el mismo metodo que
// invoca Program.cs, ver ComposicionServicios.AgregarServiciosControlHoras) y valida que el grafo
// completo es resoluble, sin infra desplegada (connection strings dummy).
//
// Root cause que cierra este guardrail (issue #219): el upgrade de Cosmos.Event* 0.1.9 -> 2.1.0
// (issue #207) dejo de auto-registrar un ITenantResolver, y ni "dotnet build" ni los tests
// unitarios existentes construyen el grafo de DI del host, asi que el hueco solo se detecto
// DESPUES del deploy, en los smoke tests contra dev (HTTP 500 en toda funcion).
//
// Limite conocido: BuildServiceProvider(ValidateOnBuild) no valida registros por factory-lambda
// (Wolverine/Marten registran varios), solo los de tipo mapeado -- de ahi la resolucion explicita
// de ICommandRouter e IPrivateEventRouter en un scope, ademas del build general.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class ComposicionServiciosTests
{
    private const string MartenConnectionStringDummy =
        "Host=dummy;Port=5432;Database=dummy;Username=dummy;Password=dummy";

    private const string ServiceBusConnectionStringDummy =
        "Endpoint=sb://dummy.servicebus.windows.net/;SharedAccessKeyName=dummy;SharedAccessKey=dummy";

    private static ServiceProvider ComponerServiceProvider()
    {
        var services = new ServiceCollection();

        services.AgregarServiciosControlHoras(
            MartenConnectionStringDummy,
            ServiceBusConnectionStringDummy,
            isDev: true);

        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Fact]
    public void AgregarServiciosControlHoras_ComponeElGrafoCompleto_CuandoLasConnectionStringsSonDummy()
    {
        var act = () => ComponerServiceProvider().Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task AgregarServiciosControlHoras_ResuelveICommandRouter_CuandoElContenedorEstaCompuesto()
    {
        // Wolverine registra dependencias scoped que solo implementan IAsyncDisposable
        // (Wolverine.Persistence.MessageStoreCollection); el scope se libera con DisposeAsync.
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => scope.ServiceProvider.GetRequiredService<ICommandRouter>();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task AgregarServiciosControlHoras_ResuelveIPrivateEventRouter_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => scope.ServiceProvider.GetRequiredService<IPrivateEventRouter>();

        act.Should().NotThrow();
    }
}
