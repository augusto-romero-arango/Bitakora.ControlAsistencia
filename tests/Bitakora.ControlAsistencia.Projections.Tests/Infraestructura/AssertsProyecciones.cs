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
    /// Issue #289 CA-4: la proyeccion concreta quedo registrada en el named store con lifecycle
    /// Async -- complementa AssertSinProyeccionesInline (que solo prueba que NADA quedo Inline; una
    /// lista de proyecciones vacia pasaria esa guarda sin decir nada sobre si la proyeccion
    /// concreta llego a registrarse). <paramref name="nombreVista"/> es el nombre de la VISTA
    /// (p. ej. "TurnoVigente"), no el de la clase de proyeccion -- verificado por decompilacion
    /// con ilspycmd contra JasperFx.Events 2.18.1 (investigacion del planner, issue #289):
    /// ISubscriptionSource.Name es la propiedad real (no "ProjectionName", que no existe en la
    /// interfaz), y Marten la deriva del tipo de documento agregado, nunca del nombre de la clase
    /// companion (p. ej. TurnoVigenteProjection).
    /// </summary>
    public static void AssertProyeccionAsyncRegistrada(this IDocumentStore store, string nombreVista) =>
        store.Options.Events.Projections()
            .Should().ContainSingle(proyeccion =>
                proyeccion.Name == nombreVista
                && proyeccion.Lifecycle == ProjectionLifecycle.Async);

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
    /// ComputarStreamId -> "{CodigoColaborador}:{Fecha:yyyy-MM-dd}" en control diario --, nunca el Guid
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

    /// <summary>
    /// Issue #268 CA-1: el named store lee el event store con el mismo modelo de tenancy con el
    /// que el write-side lo escribio (AgregarConfiguracionMartenComandos, Cosmos.EventSourcing.
    /// CritterStack 2.3.1: Events.TenancyStyle = Conjoined). Marten documenta TenancyStyle.
    /// Conjoined como un modelo opt-in que captura los eventos por tenant ("Event Store
    /// Multi-Tenancy": https://martendb.io/events/multitenancy.html) -- el lado que lee tiene que
    /// declarar el mismo modelo que el que escribio. Sin esta linea el named store queda con el
    /// default Single, un par 1 (eventos, ver contexto del issue) desalineado con el write-side.
    ///
    /// Igual que StreamIdentity, TenancyStyle esta declarada en la superficie de solo lectura
    /// (Marten.Events.IReadOnlyEventStoreOptions.TenancyStyle, get-only), asi que no hace falta
    /// castear a StoreOptions.
    /// </summary>
    public static void AssertTenancyDeEventosConjoined(this IDocumentStore store) =>
        store.Options.Events.TenancyStyle.Should().Be(TenancyStyle.Conjoined);

    /// <summary>
    /// Issue #268 CA-1: replica de Events.EventNamingStyle = SmarterTypeName (el write-side lo
    /// declara via AgregarConfiguracionMartenComandos). Hoy es inocua -- los eventos persistidos de
    /// este BC son tipos top-level, y SmarterTypeName solo desambigua tipos anidados prefijando
    /// "[tipo externo].[tipo interno]" (doc XML de JasperFx.Events) -- pero sin esta linea el named
    /// store calcularia el alias de un futuro evento anidado distinto de como lo calcula el
    /// write-side, sin ninguna senal en el build.
    /// </summary>
    public static void AssertEventNamingStyleSmarterTypeName(this IDocumentStore store) =>
        store.Options.Events.EventNamingStyle.Should().Be(EventNamingStyle.SmarterTypeName);

    /// <summary>
    /// Issue #268 CA-1: Policies.AllDocumentsAreMultiTenanted() gobierna el "par 2" (worker -> query-
    /// side, ver contexto del issue): la forma de la tabla de cualquier read model que el worker
    /// llegue a materializar. Es una politica que se aplica al registrar un documento, no una
    /// propiedad expuesta directamente -- se observa por su efecto en el mapping que Marten resuelve
    /// para un tipo cualquiera (FindOrResolveDocumentType), la unica superficie que la deja visible
    /// sin Postgres y sin que el worker tenga todavia ningun read model real registrado. TCanario es
    /// un tipo declarado en este proyecto de tests, sin relacion con ningun dominio.
    /// </summary>
    public static void AssertDocumentosMultiTenant<TCanario>(this IDocumentStore store) =>
        store.Options.FindOrResolveDocumentType(typeof(TCanario)).TenancyStyle
            .Should().Be(TenancyStyle.Conjoined);

    /// <summary>
    /// Issue #268 CA-2: deserializa <paramref name="original"/> contra el ISerializer real que este
    /// named store compuso -- el mismo objeto que Marten usaria para leer un evento persistido.
    /// Devuelve el objeto restaurado para que el llamador lo compare campo a campo contra un
    /// oraculo construido a mano (MEF-ADR-0002, no-tautologia): este helper no decide que
    /// verificar, solo ejecuta el round-trip.
    ///
    /// El eslabon que hace fallar esto si el seam esta mal armado: los eventos con AMBOS
    /// constructores privados (TurnoCreado, MarcacionRegistrada) solo se reconstruyen via el
    /// TypeInfoResolver custom que ConfiguracionSerializacion{Dominio}.ConfigurarResolver registra.
    /// Sin ese resolver -- o si UseSystemTextJsonForSerialization se invoca DESPUES de engancharlo,
    /// la "trampa del orden" que describe el issue -- STJ no tiene forma de instanciar el tipo y
    /// esto lanza NotSupportedException en vez de fallar en silencio.
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
