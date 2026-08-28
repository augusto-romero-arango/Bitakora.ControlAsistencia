using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using JasperFx.Events; // StreamIdentity, EventNamingStyle (NO Marten.Events, mismo gotcha que DaemonMode)
using JasperFx.Events.Daemon; // DaemonMode (NO Marten.Events.Daemon: compila pero deja DaemonMode sin resolver)
using JasperFx.MultiTenancy; // TenancyStyle (NO Marten.*, mismo gotcha que StreamIdentity/DaemonMode)
using Marten;

namespace Bitakora.ControlAsistencia.Projections.Infraestructura;

/// <summary>
/// Marker del named store de proyecciones del dominio Sedes (MEF-ADR-0034 seccion 2).
/// </summary>
public interface ISedesProjectionStore : IDocumentStore;

/// <summary>
/// Seam de composicion de proyecciones del dominio Sedes (issue #455;
/// MEF-ADR-0006/MEF-ADR-0034 secciones 2 y 6) -- hermano read-side de ComposicionServicios
/// (write-side, MEF-ADR-0029): fuente unica que comparten Program.cs del worker y el config-test.
///
/// Registra el named store sobre la misma conexion y el mismo schema "sedes" que ya usa (o usara)
/// el write-side (ComposicionServicios.AgregarServiciosSedes) -- el read-side no crea base ni
/// schema nuevos. El dominio nace sin ninguna proyeccion concreta: se suman aditivamente dentro
/// de este mismo AddMartenStore cuando el desglose de issues (#456-#461) materialice el primer
/// read model.
///
/// El seam se declara con modificadores de acceso y sin partial: un metodo partial sin
/// modificadores desaparece en silencio al compilar si nadie lo implementa, y ademas seria
/// implicitamente privado e inalcanzable desde el ensamblado de tests.
/// </summary>
public static class ConfiguracionMartenProjectionsSedes
{
    private const string SchemaDelDominio = "sedes";

    public static IServiceCollection ConfigurarSedes(
        this IServiceCollection services, string martenConnectionString)
    {
        services.AddMartenStore<ISedesProjectionStore>(opts =>
            {
                opts.Connection(martenConnectionString);
                opts.DatabaseSchemaName = SchemaDelDominio; // mismo schema que el write-side (MEF-ADR-0003)

                // Replica de la identidad de stream que el write-side declara por defecto via
                // Cosmos.EventSourcing.CritterStack (AgregarConfiguracionMartenComandos: Events.
                // StreamIdentity = AsString): mismo patron que Colaboradores -- el issue #455 asume
                // que SedeAggregateRoot se identificara por un valor de texto (p.ej. un codigo de
                // sede), no un Guid, hasta que el desglose #456-#461 confirme lo contrario. Sin
                // esta linea Marten aplica su default AsGuid y el daemon leeria el event store
                // (stream_id varchar) como uuid, sin encontrar ningun stream (mismo sintoma que el
                // issue #253 de ControlHoras/Programacion).
                opts.Events.StreamIdentity = StreamIdentity.AsString;

                // El event store se escribe con tenancy conjoined (AgregarConfiguracionMartenComandos,
                // Cosmos.EventSourcing.CritterStack 2.3.1) y Marten documenta TenancyStyle.Conjoined
                // como un modelo opt-in que el lado que lee debe declarar igual que el que escribio
                // ("Event Store Multi-Tenancy": https://martendb.io/events/multitenancy.html).
                opts.Events.TenancyStyle = TenancyStyle.Conjoined;

                // Replica de Events.EventNamingStyle = SmarterTypeName (idem ControlHoras/
                // Programacion/Colaboradores): hoy inocua (eventos top-level, sin anidar), pero sin
                // esta linea un futuro evento anidado calcularia un alias distinto entre write-side
                // y worker, sin ninguna senal en el build.
                opts.Events.EventNamingStyle = EventNamingStyle.SmarterTypeName;

                // Policies.AllDocumentsAreMultiTenanted() gobierna la forma de la tabla de cualquier
                // read model que este worker llegue a materializar para Sedes.
                opts.Policies.AllDocumentsAreMultiTenanted();

                // Replica de la configuracion de metadata del write-side (MEF-ADR-0034 seccion 6
                // punto 3, seccion 7): el config-test verifica exactamente estas tres. La
                // habilitacion real de las columnas es responsabilidad del write-side de este
                // dominio (issue #455, ComposicionServicios.AgregarServiciosSedes).
                opts.Events.MetadataConfig.CorrelationIdEnabled = true;
                opts.Events.MetadataConfig.CausationIdEnabled = true;
                opts.Events.MetadataConfig.HeadersEnabled = true;

                // Defensa en profundidad read-side: registra los tipos de evento persistidos de
                // Sedes en el EventGraph de este named store, para que el daemon no dependa del
                // fallback por mt_dotnet_type al leer streams preexistentes. Lista vacia al nacer
                // -- se llena junto con IdentidadEventosSedes.TiposPersistidos.
                opts.Events.AddEventTypes(IdentidadEventosSedes.TiposPersistidos);

                // Cuando SedeAggregateRoot aplique su primer evento y aparezca la primera
                // proyeccion, se agrega aqui con opts.Projections.Add<TProjection>(ProjectionLifecycle.Async)
                // -- lifecycle Async es el canonico del worker (MEF-ADR-0034 seccion 3); Inline solo
                // seria valido con justificacion explicita en el issue correspondiente.
            })
            // Registrar el store no basta: sin esta llamada el daemon queda apagado y ninguna
            // proyeccion se materializa. HotCold elige lider sobre advisory locks de PostgreSQL,
            // lo correcto para un Container App que Azure puede correr momentaneamente con mas de
            // una replica (MEF-ADR-0034 seccion 2).
            .AddAsyncDaemon(DaemonMode.HotCold);

        return services;
    }
}
