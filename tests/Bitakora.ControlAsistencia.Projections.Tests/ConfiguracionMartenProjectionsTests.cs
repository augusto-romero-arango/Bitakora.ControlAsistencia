using AwesomeAssertions;
using Bitakora.ControlAsistencia.Projections.Infraestructura;
using Bitakora.ControlAsistencia.Projections.Tests.Infraestructura;
using JasperFx.Events.Projections;
using Microsoft.Extensions.DependencyInjection;

namespace Bitakora.ControlAsistencia.Projections.Tests;

// Config-test del worker de proyecciones (MEF-ADR-0034 seccion 6, hermano de MEF-ADR-0029).
// Invoca cada Configurar{Dominio} directamente -- nunca a traves de ConfiguracionMartenProjections
// .ConfigurarEventos, que es wiring puro para Program.cs y queda fuera de esta medicion -- con una
// cadena de conexion dummy, sin necesidad de Postgres real (Marten 7+ no abre la conexion durante
// el bootstrapping del IHost). Cada dominio se cubre con las tres guardas de la seccion 6:
//   1. Guarda del partial: el named store resuelve desde el contenedor.
//   2. Ninguna proyeccion registrada con lifecycle Inline (Async es el ciclo de vida canonico).
//      Superficie reverificada por compilacion contra Marten 9.12.0: la lista de proyecciones
//      registradas se enumera con IReadOnlyStoreOptions.Events.Projections() (IReadOnlyList de
//      ISubscriptionSource, cada una con su propiedad Lifecycle) -- no con
//      StoreOptions.Projections.All, que solo existe en la superficie mutable de configuracion.
//   3. Replica exacta de Events.MetadataConfig frente al write-side de ese mismo dominio (issue #232).
public class ConfiguracionMartenProjectionsTests
{
    private const string ConnectionStringDummy = "Host=localhost;Database=dummy";

    // --- Programacion (CA-1, CA-3, CA-6, CA-7) ---

    [Fact]
    public void ConfigurarProgramacion_ResuelveIProgramacionProjectionStore_DesdeElContenedor()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.ConfigurarProgramacion(ConnectionStringDummy);

        using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IProgramacionProjectionStore>();

        store.Should().NotBeNull();
    }

    [Fact]
    public void ConfigurarProgramacion_NoRegistraNingunaProyeccionInline_EnElNamedStore()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.ConfigurarProgramacion(ConnectionStringDummy);

        using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IProgramacionProjectionStore>();

        store.Options.Events.Projections().Should().NotContain(p => p.Lifecycle == ProjectionLifecycle.Inline);
    }

    [Fact]
    public void ConfigurarProgramacion_ReplicaConfiguracionDeMetadata_DelWriteSideDeProgramacion()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.ConfigurarProgramacion(ConnectionStringDummy);

        using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IProgramacionProjectionStore>();

        store.AssertOpcionesDeEvento();
    }

    // --- ControlHoras (CA-2, CA-3, CA-6, CA-7) ---

    [Fact]
    public void ConfigurarControlHoras_ResuelveIControlHorasProjectionStore_DesdeElContenedor()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.ConfigurarControlHoras(ConnectionStringDummy);

        using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IControlHorasProjectionStore>();

        store.Should().NotBeNull();
    }

    [Fact]
    public void ConfigurarControlHoras_NoRegistraNingunaProyeccionInline_EnElNamedStore()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.ConfigurarControlHoras(ConnectionStringDummy);

        using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IControlHorasProjectionStore>();

        store.Options.Events.Projections().Should().NotContain(p => p.Lifecycle == ProjectionLifecycle.Inline);
    }

    [Fact]
    public void ConfigurarControlHoras_ReplicaConfiguracionDeMetadata_DelWriteSideDeControlHoras()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.ConfigurarControlHoras(ConnectionStringDummy);

        using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IControlHorasProjectionStore>();

        store.AssertOpcionesDeEvento();
    }
}
