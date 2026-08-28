using System.Text.Json.Serialization.Metadata;
using Bitakora.ControlAsistencia.Projections.Sedes;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using JasperFx.Events; // StreamIdentity, EventNamingStyle (NO Marten.Events, mismo gotcha que DaemonMode)
using JasperFx.Events.Daemon; // DaemonMode (NO Marten.Events.Daemon: compila pero deja DaemonMode sin resolver)
using JasperFx.Events.Projections; // ProjectionLifecycle (NO Marten.*, mismo gotcha que DaemonMode/StreamIdentity)
using JasperFx.MultiTenancy; // TenancyStyle (NO Marten.*, mismo gotcha que StreamIdentity/DaemonMode)
using Marten;
using Weasel.Core; // EnumStorage, Casing (NO Marten.*: viven en Weasel.Core)

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
                // fallback por mt_dotnet_type al leer streams preexistentes. La lista se lee del
                // mismo artefacto que el write-side (issue #456: SedeRegistrada), asi que ambos
                // lados no pueden divergir.
                opts.Events.AddEventTypes(IdentidadEventosSedes.TiposPersistidos);

                // Issue #456 (par 1 de MEF-ADR-0034 seccion 6, fila "Serializador"): el dominio
                // estrena su primer evento persistido, asi que el read-side deja de poder confiar
                // en un default. EnumStorage.AsInteger/Casing.Default son los que fija el write-side
                // via AgregarConfiguracionMartenComandos (Cosmos.EventSourcing.CritterStack 2.3.1);
                // se declaran igual para no depender de un default de Marten que un upgrade futuro
                // podria mover. Serializador y resolver en una sola llamada: UseSystemTextJsonFor-
                // Serialization construye un serializador NUEVO y lo reemplaza, asi que invocarlo
                // despues de un stj.Configure(...) perderia el TypeInfoResolver en silencio ("la
                // trampa del orden", issue #268). Fuente unica con el write-side (MEF-ADR-0029): se
                // invoca la misma clase, nunca una copia.
                opts.UseSystemTextJsonForSerialization(EnumStorage.AsInteger, Casing.Default, jsonOptions =>
                {
                    var resolver = new DefaultJsonTypeInfoResolver();
                    ConfiguracionSerializacionSedes.ConfigurarResolver(resolver);
                    jsonOptions.TypeInfoResolver = resolver;
                });

                // Issue #461: primera proyeccion concreta del dominio -- N1 (SingleStreamProjection
                // sobre el stream de SedeAggregateRoot). Async es el lifecycle canonico del worker
                // (MEF-ADR-0034 seccion 3); Inline solo seria valido con justificacion explicita en
                // el issue correspondiente.
                opts.Projections.Add<FichaSedeProjection>(ProjectionLifecycle.Async);
            })
            // Registrar el store no basta: sin esta llamada el daemon queda apagado y ninguna
            // proyeccion se materializa. HotCold elige lider sobre advisory locks de PostgreSQL,
            // lo correcto para un Container App que Azure puede correr momentaneamente con mas de
            // una replica (MEF-ADR-0034 seccion 2).
            .AddAsyncDaemon(DaemonMode.HotCold);

        return services;
    }
}
