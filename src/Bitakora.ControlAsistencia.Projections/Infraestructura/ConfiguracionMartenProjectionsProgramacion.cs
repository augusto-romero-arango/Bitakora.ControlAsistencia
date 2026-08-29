using System.Text.Json.Serialization.Metadata;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Projections.Programacion;
using JasperFx.Events; // StreamIdentity, EventNamingStyle (NO Marten.Events, mismo gotcha que DaemonMode)
using JasperFx.Events.Daemon; // DaemonMode (NO Marten.Events.Daemon: compila pero deja DaemonMode sin resolver)
using JasperFx.Events.Projections; // ProjectionLifecycle (NO Marten.*, mismo gotcha que DaemonMode/StreamIdentity)
using JasperFx.MultiTenancy; // TenancyStyle (NO Marten.*, mismo gotcha que StreamIdentity/DaemonMode)
using Marten;
using Weasel.Core; // EnumStorage, Casing (NO Marten.*: viven en Weasel.Core)

namespace Bitakora.ControlAsistencia.Projections.Infraestructura;

/// <summary>
/// Marker del named store de proyecciones del dominio Programacion (MEF-ADR-0034 seccion 2).
/// </summary>
public interface IProgramacionProjectionStore : IDocumentStore;

/// <summary>
/// Seam de composicion de proyecciones del dominio Programacion (MEF-ADR-0006/MEF-ADR-0034
/// secciones 2 y 6) -- hermano read-side de ComposicionServicios (write-side, MEF-ADR-0029):
/// fuente unica que comparten Program.cs del worker y el config-test.
///
/// Registra el named store sobre la misma conexion y el mismo schema "programacion" que ya usa
/// el write-side (ComposicionServicios.AgregarServiciosProgramacion) -- el read-side no crea
/// base ni schema nuevos, solo re-declara del lado lectura lo que el dominio ya posee del lado
/// escritura. Sin ninguna proyeccion concreta todavia: las agrega projection-implementer sobre
/// este mismo seam, siempre con lifecycle Async (MEF-ADR-0034 seccion 3).
///
/// El seam se declara con modificadores de acceso y sin partial: un metodo partial sin
/// modificadores desaparece en silencio al compilar si nadie lo implementa, y ademas seria
/// implicitamente privado e inalcanzable desde el ensamblado de tests.
/// </summary>
public static class ConfiguracionMartenProjectionsProgramacion
{
    private const string SchemaDelDominio = "programacion";

    public static IServiceCollection ConfigurarProgramacion(
        this IServiceCollection services, string martenConnectionString)
    {
        services.AddMartenStore<IProgramacionProjectionStore>(opts =>
            {
                opts.Connection(martenConnectionString);
                opts.DatabaseSchemaName = SchemaDelDominio; // mismo schema que el write-side (MEF-ADR-0003)

                // Replica de la identidad de stream que el write-side ya declara
                // (Cosmos.EventSourcing.CritterStack 2.1.0, AgregarConfiguracionMartenComandos:
                // Events.StreamIdentity = AsString): los stream keys de este dominio son siempre
                // strings -- e.Id.ToString() en la raiz de solicitud, evento.TurnoId.ToString() en
                // el catalogo de turnos. Sin esta linea Marten aplica su default AsGuid ("Event
                // Store Configuration" -> "Stream Identity" en
                // https://martendb.io/events/configuration.html#stream-identity) y el daemon leeria
                // el event store (stream_id varchar) como uuid, sin encontrar ningun stream (#253).
                opts.Events.StreamIdentity = StreamIdentity.AsString;

                // Issue #268 CA-1 (MEF-ADR-0034 seccion 6 enmendada por #447 del marco, "par 1"):
                // el event store se escribio con tenancy conjoined
                // (AgregarConfiguracionMartenComandos, Cosmos.EventSourcing.CritterStack 2.3.1) y
                // Marten documenta TenancyStyle.Conjoined como un modelo opt-in ("Event Store
                // Multi-Tenancy": https://martendb.io/events/multitenancy.html) que el lado que lee
                // debe declarar igual que el que escribio. Sin esta linea el daemon queda con el
                // default Single, desalineado del write-side.
                opts.Events.TenancyStyle = TenancyStyle.Conjoined;

                // Issue #268 CA-1: replica de Events.EventNamingStyle = SmarterTypeName. Hoy inocua
                // (los eventos persistidos son tipos top-level; SmarterTypeName solo desambigua
                // tipos anidados, doc XML de JasperFx.Events), pero sin esta linea un futuro evento
                // anidado calcularia un alias distinto entre write-side y worker, sin ninguna senal
                // en el build.
                opts.Events.EventNamingStyle = EventNamingStyle.SmarterTypeName;

                // Issue #268 CA-1 ("par 2"): Policies.AllDocumentsAreMultiTenanted() gobierna la
                // forma de la tabla de cualquier read model que este worker llegue a materializar
                // -- no el event store, sino el query-side del Function App (session.LoadAsync).
                opts.Policies.AllDocumentsAreMultiTenanted();

                // Replica de la configuracion de metadata del write-side (MEF-ADR-0034 seccion 6
                // punto 3, seccion 7): el config-test verifica exactamente estas tres. La
                // habilitacion real de las columnas es responsabilidad del write-side de este
                // dominio (issue #232).
                opts.Events.MetadataConfig.CorrelationIdEnabled = true;
                opts.Events.MetadataConfig.CausationIdEnabled = true;
                opts.Events.MetadataConfig.HeadersEnabled = true;

                // Issue #277: defensa en profundidad read-side. Registra los tipos de evento
                // persistidos de Programacion en el EventGraph de este named store, para que el
                // daemon no dependa del fallback por mt_dotnet_type al leer streams preexistentes
                // (issue #237 seccion "Consecuencia asumida").
                opts.Events.AddEventTypes(IdentidadEventosProgramacion.TiposPersistidos);

                // Issue #268 CA-2: serializador y resolver en una sola llamada. A diferencia del
                // write-side (ComposicionServicios.cs), que engancha el resolver despues via
                // `if (opts.Serializer() is SystemTextJsonSerializer stj) stj.Configure(...)`
                // porque el paquete ya construyo el serializador, aqui se controla todo el lambda
                // de una vez -- UseSystemTextJsonForSerialization construye un serializador NUEVO
                // y lo reemplaza, asi que si se invocara despues de un stj.Configure(...) previo
                // ("la trampa del orden") el TypeInfoResolver se perderia en runtime, sin error de
                // compilacion. EnumStorage.AsInteger/Casing.Default son los defaults del propio
                // metodo -- se declaran igual, no porque hoy diverjan (ningun enum persistido
                // cruza este ensamblado, CA-ADR-0029), sino para no depender de un default de
                // Marten que un upgrade futuro podria mover.
                // Fuente unica con el write-side (MEF-ADR-0029): se invoca la misma clase
                // ConfiguracionSerializacionProgramacion, nunca una copia.
                opts.UseSystemTextJsonForSerialization(EnumStorage.AsInteger, Casing.Default, jsonOptions =>
                {
                    var resolver = new DefaultJsonTypeInfoResolver();
                    ConfiguracionSerializacionProgramacion.ConfigurarResolver(resolver);
                    jsonOptions.TypeInfoResolver = resolver;
                });

                // Async es el lifecycle canonico del worker (MEF-ADR-0034 seccion 3). Registrarla
                // aqui es tambien lo que hace que Marten fije mt_version bigint sobre FichaTurno.
                opts.Projections.Add<FichaTurnoProjection>(ProjectionLifecycle.Async);
            })
            // Registrar el store no basta: sin esta llamada el daemon queda apagado y ninguna
            // proyeccion se materializa. HotCold elige lider sobre advisory locks de PostgreSQL,
            // lo correcto para un Container App que Azure puede correr momentaneamente con mas
            // de una replica (MEF-ADR-0034 seccion 2).
            .AddAsyncDaemon(DaemonMode.HotCold);

        return services;
    }
}
