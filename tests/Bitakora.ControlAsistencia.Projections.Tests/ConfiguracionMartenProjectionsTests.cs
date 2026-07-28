using AwesomeAssertions;
using Bitakora.ControlAsistencia.Projections.Infraestructura;
using Bitakora.ControlAsistencia.Projections.Tests.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace Bitakora.ControlAsistencia.Projections.Tests;

// Config-test del worker de proyecciones (MEF-ADR-0034 seccion 6, hermano de MEF-ADR-0029).
// Invoca cada Configurar{Dominio} directamente -- nunca a traves de ConfiguracionMartenProjections
// .ConfigurarEventos, que es wiring puro para Program.cs y queda fuera de esta medicion -- con una
// cadena de conexion dummy, sin necesidad de Postgres real (Marten 7+ no abre la conexion durante
// el bootstrapping del IHost). Cada dominio se cubre con las mismas guardas: el named store
// resuelve del contenedor, apunta al schema del write-side, replica su metadata de evento, no
// tiene ninguna proyeccion Inline y corre su daemon en HotCold. La superficie de Marten que cada
// una interroga vive en AssertsProyecciones.
public class ConfiguracionMartenProjectionsTests
{
    private const string ConnectionStringDummy = "Host=localhost;Database=dummy";

    private static ServiceProvider CrearProvider(Action<IServiceCollection> configurarDominio)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        configurarDominio(services);

        return services.BuildServiceProvider();
    }

    private static ServiceProvider ProviderDeProgramacion() =>
        CrearProvider(services => services.ConfigurarProgramacion(ConnectionStringDummy));

    private static ServiceProvider ProviderDeControlHoras() =>
        CrearProvider(services => services.ConfigurarControlHoras(ConnectionStringDummy));

    // --- Programacion (CA-1, CA-3, CA-6, CA-7) ---

    [Fact]
    public void ConfigurarProgramacion_ResuelveElNamedStoreDelDominio()
    {
        using var provider = ProviderDeProgramacion();

        var store = provider.GetService<IProgramacionProjectionStore>();

        store.Should().NotBeNull();
    }

    [Fact]
    public void ConfigurarProgramacion_RegistraElNamedStoreSobreElSchemaDeProgramacion()
    {
        using var provider = ProviderDeProgramacion();

        provider.GetRequiredService<IProgramacionProjectionStore>().AssertSchema("programacion");
    }

    [Fact]
    public void ConfigurarProgramacion_ReplicaLaMetadataDeEventoDelWriteSide()
    {
        using var provider = ProviderDeProgramacion();

        provider.GetRequiredService<IProgramacionProjectionStore>().AssertOpcionesDeEvento();
    }

    [Fact]
    public void ConfigurarProgramacion_NoRegistraNingunaProyeccionInline()
    {
        using var provider = ProviderDeProgramacion();

        provider.GetRequiredService<IProgramacionProjectionStore>().AssertSinProyeccionesInline();
    }

    [Fact]
    public void ConfigurarProgramacion_EnciendeElDaemonEnModoHotCold()
    {
        using var provider = ProviderDeProgramacion();

        provider.AssertDaemonHotCold<IProgramacionProjectionStore>();
    }

    // Issue #253 (CA-1, CA-3, CA-4): el named store de Programacion debe leer el event store con
    // la misma identidad de stream que su write-side (SolicitudProgramacionAggregateRoot.Id =
    // e.Id.ToString(), CatalogoTurnos con TurnoId.ToString()), no con el default AsGuid de Marten.
    [Fact]
    public void ConfigurarProgramacion_DeclaraLaStreamIdentityComoString()
    {
        using var provider = ProviderDeProgramacion();

        provider.GetRequiredService<IProgramacionProjectionStore>().AssertStreamIdentityAsString();
    }

    // --- ControlHoras (CA-2, CA-3, CA-6, CA-7) ---

    [Fact]
    public void ConfigurarControlHoras_ResuelveElNamedStoreDelDominio()
    {
        using var provider = ProviderDeControlHoras();

        var store = provider.GetService<IControlHorasProjectionStore>();

        store.Should().NotBeNull();
    }

    [Fact]
    public void ConfigurarControlHoras_RegistraElNamedStoreSobreElSchemaDeControlHoras()
    {
        using var provider = ProviderDeControlHoras();

        provider.GetRequiredService<IControlHorasProjectionStore>().AssertSchema("control_horas");
    }

    [Fact]
    public void ConfigurarControlHoras_ReplicaLaMetadataDeEventoDelWriteSide()
    {
        using var provider = ProviderDeControlHoras();

        provider.GetRequiredService<IControlHorasProjectionStore>().AssertOpcionesDeEvento();
    }

    [Fact]
    public void ConfigurarControlHoras_NoRegistraNingunaProyeccionInline()
    {
        using var provider = ProviderDeControlHoras();

        provider.GetRequiredService<IControlHorasProjectionStore>().AssertSinProyeccionesInline();
    }

    [Fact]
    public void ConfigurarControlHoras_EnciendeElDaemonEnModoHotCold()
    {
        using var provider = ProviderDeControlHoras();

        provider.AssertDaemonHotCold<IControlHorasProjectionStore>();
    }

    // Issue #253 (CA-2, CA-3, CA-4): el named store de ControlHoras debe leer el event store con
    // la misma identidad de stream que su write-side (ControlDiarioAggregateRoot.ComputarStreamId
    // devuelve "{EmpleadoId}:{Fecha:yyyy-MM-dd}", nunca un Guid), no con el default AsGuid de
    // Marten.
    [Fact]
    public void ConfigurarControlHoras_DeclaraLaStreamIdentityComoString()
    {
        using var provider = ProviderDeControlHoras();

        provider.GetRequiredService<IControlHorasProjectionStore>().AssertStreamIdentityAsString();
    }

    // --- Seam de nivel BC (CA-4) ---

    // Las guardas de arriba invocan cada Configurar{Dominio} directamente, asi que quedan verdes
    // aunque nadie encadene esa llamada en ConfigurarEventos -- y Program.cs solo invoca
    // ConfigurarEventos. Sin esta guarda, un dominio con su seam implementado pero sin encadenar
    // compila limpio, pasa el resto del config-test y su daemon nunca corre en produccion.
    [Fact]
    public void ConfigurarEventos_RegistraElNamedStoreDeCadaDominioDelBc()
    {
        using var provider = CrearProvider(services => services.ConfigurarEventos(ConnectionStringDummy));

        provider.GetService<IProgramacionProjectionStore>().Should().NotBeNull();
        provider.GetService<IControlHorasProjectionStore>().Should().NotBeNull();
    }
}
