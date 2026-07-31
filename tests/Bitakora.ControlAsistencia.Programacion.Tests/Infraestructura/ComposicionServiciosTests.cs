// Issue #221: guardrail de CI que compone el contenedor DI real del dominio (el mismo metodo que
// invoca Program.cs, ver ComposicionServicios.AgregarServiciosProgramacion) y valida que el grafo
// completo es resoluble, sin infra desplegada (connection strings dummy).
//
// Root cause que cierra este guardrail (issue #219): el upgrade de Cosmos.Event* 0.1.9 -> 2.1.0
// (issue #207) dejo de auto-registrar un ITenantResolver, y ni "dotnet build" ni los tests
// unitarios existentes construyen el grafo de DI del host, asi que el hueco solo se detecto
// DESPUES del deploy, en los smoke tests contra dev (HTTP 500 en toda funcion).
//
// Programacion no registra IPrivateEventRouter ni IQueryRouter hoy (ver Notas tecnicas del
// issue #221): solo se resuelve explicitamente ICommandRouter, el unico router critico
// efectivamente registrado en este dominio.
//
// Issue #232 (MEF-ADR-0034 seccion 7): Marten deja deshabilitadas por defecto las tres columnas
// de metadata de evento (CorrelationId/CausationId/Headers) -- sin opt-in explicito la columna ni
// siquiera se crea en la tabla de eventos. Este test verifica el opt-in sobre el IDocumentStore
// resuelto del contenedor real (sin Postgres: el DocumentStore no abre conexion en bootstrap,
// solo en la primera operacion real -- Marten 7+). La cadena de lectura es de solo lectura y sin
// downcast: IDocumentStore.Options (IReadOnlyStoreOptions) -> Events (IReadOnlyEventStoreOptions)
// -> MetadataConfig (IReadonlyMetadataConfig).

using System.Text;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Infraestructura;

public class ComposicionServiciosTests
{
    private const string MartenConnectionStringDummy =
        "Host=dummy;Port=5432;Database=dummy;Username=dummy;Password=dummy";

    private const string ServiceBusConnectionStringDummy =
        "Endpoint=sb://dummy.servicebus.windows.net/;SharedAccessKeyName=dummy;SharedAccessKey=dummy";

    private static ServiceProvider ComponerServiceProvider()
    {
        var services = new ServiceCollection();

        services.AgregarServiciosProgramacion(
            MartenConnectionStringDummy,
            ServiceBusConnectionStringDummy,
            isDev: true);

        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Fact]
    public void AgregarServiciosProgramacion_ComponeElGrafoCompleto_CuandoLasConnectionStringsSonDummy()
    {
        var act = () => ComponerServiceProvider().Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task AgregarServiciosProgramacion_ResuelveICommandRouter_CuandoElContenedorEstaCompuesto()
    {
        // Wolverine registra dependencias scoped que solo implementan IAsyncDisposable
        // (Wolverine.Persistence.MessageStoreCollection); el scope se libera con DisposeAsync.
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var act = () => scope.ServiceProvider.GetRequiredService<ICommandRouter>();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task AgregarServiciosProgramacion_HabilitaColumnasDeMetadataDeEvento_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        var metadataConfig = store.Options.Events.MetadataConfig;

        metadataConfig.CorrelationIdEnabled.Should().BeTrue();
        metadataConfig.CausationIdEnabled.Should().BeTrue();
        metadataConfig.HeadersEnabled.Should().BeTrue();
    }

    // Issue #232 CA-5: las tres banderas de metadata comparten el mismo callback ConfigureMarten que
    // registra el resolver de serializacion custom, asi que un edit futuro de ese bloque puede tumbar
    // la serializacion sin que el test de metadata se ponga rojo. Los round-trip existentes
    // (TurnoCreadoSerializacionTests) NO cubren este riesgo: replican las opciones de Marten a mano
    // -- una ruta paralela que no atraviesa el contenedor. Este test ejercita el ISerializer que el
    // store realmente compuso. Importa porque el `if (options.Serializer() is SystemTextJsonSerializer)`
    // del wiring omite el resolver EN SILENCIO si el serializador deja de ser STJ, y SubFranja
    // (campos privados, sin propiedades publicas) no sobrevive STJ vanilla.
    [Fact]
    public async Task AgregarServiciosProgramacion_ConservaLaSerializacionCustom_CuandoTambienHabilitaMetadataDeEvento()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var serializador = scope.ServiceProvider.GetRequiredService<IDocumentStore>().Options.Serializer();
        var original = SubFranja.Crear(new TimeOnly(6, 0), new TimeOnly(16, 0));

        using var json = new MemoryStream(Encoding.UTF8.GetBytes(serializador.ToJson(original)));
        var restaurado = serializador.FromJson<SubFranja>(json);

        restaurado.Should().Be(original);
        restaurado.ToString().Should().Be(original.ToString());
    }

    // Issue #277 CA-2/CA-4: el #237 movio los eventos persistidos a este ensamblado (namespace y
    // assembly nuevos) sin registrar su tipo en el EventGraph. Toda lectura quedo dependiendo del
    // fallback por mt_dotnet_type -- roto para el nuevo namespace -- en vez de resolver por alias
    // (columna "type" de mt_events). Este guardrail detecta el olvido sobre el store real que
    // compone el contenedor (mismo store que ya usan las guardas de arriba), sin Postgres.
    //
    // Los tipos esperados se listan literalmente (oraculo independiente, MEF-ADR-0002): si se
    // leyeran de IdentidadEventosProgramacion.TiposPersistidos, el guardrail quedaria acoplado al
    // mismo artefacto que CA-1 ya verifica, y ademas AwesomeAssertions.Contain() lanza
    // ArgumentException con una coleccion "esperada" vacia en vez de fallar semanticamente --
    // el stub de fase roja (issue #277) deja esa lista vacia a proposito.
    [Fact]
    public async Task AgregarServiciosProgramacion_RegistraLosTiposDeEventoPersistidos_CuandoElContenedorEstaCompuesto()
    {
        await using var provider = ComponerServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();

        store.AssertEventosPersistidosRegistrados([typeof(TurnoCreado), typeof(ProgramacionTurnoSolicitada)]);
    }
}
