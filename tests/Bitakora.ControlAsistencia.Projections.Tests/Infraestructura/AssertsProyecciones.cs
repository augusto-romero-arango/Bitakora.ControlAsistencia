using AwesomeAssertions;
using JasperFx.Events;
using JasperFx.Events.Daemon;
using JasperFx.Events.Projections;
using Marten;
using Marten.Events.Daemon.Coordination;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bitakora.ControlAsistencia.Projections.Tests.Infraestructura;

/// <summary>
/// Helpers reutilizables del config-test del worker (MEF-ADR-0034 seccion 6): encapsulan la
/// superficie exacta de Marten 9.12.0 que hay que interrogar para cada guarda, de modo que el
/// archivo de tests exprese que se verifica y no como. Invocar sobre cada named store que el
/// config-test resuelva del contenedor.
/// </summary>
public static class AssertsProyecciones
{
    /// <summary>
    /// Guarda 3: el named store replica exactamente la configuracion de metadata de evento que
    /// exige el write-side de ese mismo dominio (MEF-ADR-0034 seccion 7). Una divergencia entre
    /// ambos lados rompe la proyeccion en runtime, no en el build.
    /// </summary>
    public static void AssertOpcionesDeEvento(this IDocumentStore store)
    {
        var metadata = store.Options.Events.MetadataConfig;

        metadata.CorrelationIdEnabled.Should().BeTrue();
        metadata.CausationIdEnabled.Should().BeTrue();
        metadata.HeadersEnabled.Should().BeTrue();
    }

    /// <summary>
    /// El named store apunta al mismo schema que el write-side del dominio ya usa como event
    /// store (MEF-ADR-0034 seccion 2). Con un schema equivocado el daemon leeria de una tabla de
    /// eventos vacia y las proyecciones nunca se actualizarian, sin ningun error visible.
    /// </summary>
    public static void AssertSchema(this IDocumentStore store, string schemaEsperado)
    {
        store.Options.DatabaseSchemaName.Should().Be(schemaEsperado);
        store.Options.Events.DatabaseSchemaName.Should().Be(schemaEsperado);
    }

    /// <summary>
    /// Guarda 2: ninguna proyeccion del named store quedo con lifecycle Inline -- Async es el
    /// ciclo de vida canonico del worker y una Inline aqui esta mal ubicada (MEF-ADR-0034
    /// seccion 3). La lista se enumera con IReadOnlyStoreOptions.Events.Projections() (metodo,
    /// IReadOnlyList de ISubscriptionSource); store.Options.Projections.All no existe en la
    /// superficie de solo lectura que expone IDocumentStore.Options.
    /// </summary>
    public static void AssertSinProyeccionesInline(this IDocumentStore store) =>
        store.Options.Events.Projections()
            .Should().NotContain(proyeccion => proyeccion.Lifecycle == ProjectionLifecycle.Inline);

    /// <summary>
    /// El daemon del named store quedo encendido y en modo HotCold (MEF-ADR-0034 seccion 2):
    /// registrar el store no basta -- sin AddAsyncDaemon encadenado el worker arranca y nunca
    /// materializa nada, y con DaemonMode.Solo dos replicas simultaneas procesarian los mismos
    /// eventos por duplicado. Marten registra un ProjectionCoordinator tipado por el marker del
    /// store; el modo elegido solo se expone en la superficie mutable (StoreOptions), de ahi la
    /// conversion.
    /// </summary>
    public static void AssertDaemonHotCold<TStore>(this IServiceProvider provider)
        where TStore : class, IDocumentStore
    {
        provider.GetServices<IHostedService>()
            .Should().ContainSingle(servicio => servicio is ProjectionCoordinator<TStore>);

        var opciones = (StoreOptions)provider.GetRequiredService<TStore>().Options;
        opciones.Projections.AsyncMode.Should().Be(DaemonMode.HotCold);
    }

    /// <summary>
    /// El named store lee el event store con la misma identidad de stream que el write-side ya
    /// declara (Cosmos.EventSourcing.CritterStack 2.1.0: Events.StreamIdentity = AsString): los
    /// stream keys de este BC son siempre strings -- e.Id.ToString() en la raiz de solicitud,
    /// ComputarStreamId -> "{EmpleadoId}:{Fecha:yyyy-MM-dd}" en control diario --, nunca el Guid
    /// crudo. Con el default de Marten (AsGuid cuando nadie lo configura, docs "Event Store
    /// Configuration" -> "Stream Identity": https://martendb.io/events/configuration.html#stream-identity)
    /// el daemon leeria stream_id varchar como si fuera uuid y no encontraria ningun stream.
    ///
    /// A diferencia de AsyncMode (AssertDaemonHotCold), que solo existe en la superficie mutable,
    /// StreamIdentity si esta declarada en la de solo lectura que devuelve IDocumentStore.Options
    /// (Marten.Events.IReadOnlyEventStoreOptions.StreamIdentity, get-only -- verificado por
    /// decompilacion contra Marten 9.12.0 / JasperFx.Events 2.18.1), asi que aqui no hace falta
    /// castear a StoreOptions.
    ///
    /// MEF-ADR-0034 seccion 6 todavia no enumera esta guarda junto a las otras tres: candidata a
    /// enmienda de esa seccion en el harness (issue #253).
    /// </summary>
    public static void AssertStreamIdentityAsString(this IDocumentStore store) =>
        store.Options.Events.StreamIdentity.Should().Be(StreamIdentity.AsString);
}
