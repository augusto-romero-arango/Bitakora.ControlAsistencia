using System.Text;
using AwesomeAssertions;
using JasperFx.Events;
using JasperFx.Events.Daemon;
using JasperFx.Events.Projections;
using JasperFx.MultiTenancy; // TenancyStyle (NO Marten.*, mismo gotcha que StreamIdentity/DaemonMode)
using Marten;
using Marten.Events.Daemon.Coordination;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bitakora.ControlAsistencia.Projections.Tests.Infraestructura;

/// <summary>
/// Helpers del config-test del worker (MEF-ADR-0034 seccion 6). Invocar sobre cada named store que el
/// config-test resuelva del contenedor.
/// </summary>
public static class AssertsProyecciones
{
    /// <summary>
    /// El named store replica la metadata de evento que exige el write-side de su dominio (MEF-ADR-0034
    /// seccion 7): una divergencia rompe la proyeccion en runtime, no en el build.
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
    /// Ninguna proyeccion del named store es Inline: Async es el ciclo de vida del worker (MEF-ADR-0034
    /// seccion 3). store.Options.Projections.All no existe en la superficie de solo lectura; se
    /// enumera con Events.Projections().
    /// </summary>
    public static void AssertSinProyeccionesInline(this IDocumentStore store) =>
        store.Options.Events.Projections()
            .Should().NotContain(proyeccion => proyeccion.Lifecycle == ProjectionLifecycle.Inline);

    /// <summary>
    /// La proyeccion quedo registrada como Async; complementa AssertSinProyeccionesInline, que pasa con
    /// una lista vacia. <paramref name="nombreVista"/> es el nombre de la vista (p. ej. "TurnoVigente"),
    /// no el de la clase de proyeccion: Marten deriva ISubscriptionSource.Name del tipo de documento
    /// (verificado por decompilacion contra JasperFx.Events 2.18.1).
    /// </summary>
    public static void AssertProyeccionAsyncRegistrada(this IDocumentStore store, string nombreVista) =>
        store.Options.Events.Projections()
            .Should().ContainSingle(proyeccion =>
                proyeccion.Name == nombreVista
                && proyeccion.Lifecycle == ProjectionLifecycle.Async);

    /// <summary>
    /// El daemon del named store quedo encendido y en modo HotCold (MEF-ADR-0034 seccion 2): sin
    /// AddAsyncDaemon el worker arranca y nunca materializa nada, y con DaemonMode.Solo dos replicas
    /// procesarian los mismos eventos por duplicado. El modo solo se expone en StoreOptions (mutable),
    /// de ahi la conversion.
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
    /// El daemon del named store salta un hueco de secuencia estancado tras 5 minutos aunque vea
    /// transacciones vivas. El 5 es literal (oraculo independiente, MEF-ADR-0002): no leerlo de la
    /// fuente de produccion. Como AsyncMode, la opcion solo se expone en StoreOptions.
    /// </summary>
    public static void AssertSaltoDeHuecosEstancadosEnCincoMinutos(this IDocumentStore store)
    {
        var opciones = (StoreOptions)store.Options;

        opciones.Projections.SkipStaleGapsDespiteLiveTransactionsAfter
            .Should().Be(TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// El named store lee con la identidad de stream del write-side (StreamIdentity = AsString): los
    /// stream keys del BC son strings, nunca el Guid crudo. Con el default de Marten (AsGuid,
    /// https://martendb.io/events/configuration.html#stream-identity) el daemon leeria stream_id varchar
    /// como uuid y no encontraria ningun stream. StreamIdentity esta en la superficie de solo lectura
    /// (verificado contra Marten 9.12.0), asi que no hace falta castear a StoreOptions.
    /// </summary>
    public static void AssertStreamIdentityAsString(this IDocumentStore store) =>
        store.Options.Events.StreamIdentity.Should().Be(StreamIdentity.AsString);

    /// <summary>
    /// El named store lee con el modelo de tenancy con que el write-side escribio (TenancyStyle =
    /// Conjoined, opt-in en Marten: https://martendb.io/events/multitenancy.html); sin esto queda el
    /// default Single. Como StreamIdentity, esta en la superficie de solo lectura.
    /// </summary>
    public static void AssertTenancyDeEventosConjoined(this IDocumentStore store) =>
        store.Options.Events.TenancyStyle.Should().Be(TenancyStyle.Conjoined);

    /// <summary>
    /// Replica EventNamingStyle = SmarterTypeName del write-side. Hoy es inocua (solo desambigua tipos
    /// anidados y los eventos del BC son top-level), pero sin ella el named store calcularia el alias
    /// de un futuro evento anidado distinto que el write-side, sin senal en el build.
    /// </summary>
    public static void AssertEventNamingStyleSmarterTypeName(this IDocumentStore store) =>
        store.Options.Events.EventNamingStyle.Should().Be(EventNamingStyle.SmarterTypeName);

    /// <summary>
    /// Policies.AllDocumentsAreMultiTenanted() se aplica al registrar un documento, no es una propiedad
    /// expuesta: se observa en el mapping que Marten resuelve para un tipo (FindOrResolveDocumentType),
    /// sin Postgres. TCanario es un tipo de este proyecto de tests sin relacion con ningun dominio.
    /// </summary>
    public static void AssertDocumentosMultiTenant<TCanario>(this IDocumentStore store) =>
        store.Options.FindOrResolveDocumentType(typeof(TCanario)).TenancyStyle
            .Should().Be(TenancyStyle.Conjoined);

    /// <summary>
    /// Deserializa <paramref name="original"/> con el ISerializer real del named store y devuelve el
    /// objeto restaurado para compararlo contra un oraculo armado a mano (MEF-ADR-0002).
    ///
    /// Los eventos con ambos constructores privados (TurnoCreado, MarcacionRegistrada) solo se
    /// reconstruyen via el TypeInfoResolver de ConfiguracionSerializacion{Dominio}.ConfigurarResolver.
    /// Sin el, o si UseSystemTextJsonForSerialization se invoca despues de engancharlo, esto lanza
    /// NotSupportedException.
    /// </summary>
    public static TEvento DeserializarConResolverDeSerializacionCustom<TEvento>(
        this IDocumentStore store, TEvento original)
        where TEvento : notnull
    {
        var serializador = store.Options.Serializer();

        using var flujoJson = new MemoryStream(Encoding.UTF8.GetBytes(serializador.ToJson(original)));
        return serializador.FromJson<TEvento>(flujoJson);
    }
}
