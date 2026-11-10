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
/// Seam de proyecciones de Programacion: fuente unica que comparten Program.cs del worker y el
/// config-test. Usa la misma conexion y schema que el write-side y re-declara del lado lectura lo
/// que el write-side ya configura (MEF-ADR-0034 seccion 6). Los seams hermanos (ControlHoras,
/// Colaboradores, Sedes) repiten estas lineas; su porque se documenta aqui.
///
/// Sin partial: un metodo partial sin modificadores desaparece en silencio si nadie lo implementa y
/// seria privado, inalcanzable desde los tests.
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

                // Los stream keys son strings. Con el default AsGuid de Marten
                // (https://martendb.io/events/configuration.html#stream-identity) el daemon leeria
                // stream_id varchar como uuid y no encontraria ningun stream.
                opts.Events.StreamIdentity = StreamIdentity.AsString;

                // Conjoined es opt-in y el lado que lee debe declararlo igual que el que escribio
                // (https://martendb.io/events/multitenancy.html); sin esta linea el daemon queda en
                // Single, desalineado del write-side.
                opts.Events.TenancyStyle = TenancyStyle.Conjoined;

                // Hoy inocua (eventos top-level), pero sin ella un evento anidado calcularia un alias
                // distinto entre write-side y worker, sin ninguna senal en el build.
                opts.Events.EventNamingStyle = EventNamingStyle.SmarterTypeName;

                // Gobierna la forma de la tabla de los read models que consulta el Function App, no
                // el event store.
                opts.Policies.AllDocumentsAreMultiTenanted();

                opts.TolerarHuecosEstancados();

                // El config-test verifica estas tres; las columnas las habilita el write-side.
                opts.Events.MetadataConfig.CorrelationIdEnabled = true;
                opts.Events.MetadataConfig.CausationIdEnabled = true;
                opts.Events.MetadataConfig.HeadersEnabled = true;

                // Para que el daemon no dependa del fallback por mt_dotnet_type al leer streams
                // preexistentes.
                opts.Events.AddEventTypes(IdentidadEventosProgramacion.TiposPersistidos);

                // Serializador y resolver en una sola llamada: UseSystemTextJsonForSerialization
                // reemplaza el serializador, asi que despues de un stj.Configure(...) el
                // TypeInfoResolver se perderia en runtime, sin error de compilacion. EnumStorage y
                // Casing van explicitos para no depender de un default que un upgrade de Marten podria
                // mover. Misma clase de serializacion que el write-side, nunca una copia.
                opts.UseSystemTextJsonForSerialization(EnumStorage.AsInteger, Casing.Default, jsonOptions =>
                {
                    var resolver = new DefaultJsonTypeInfoResolver();
                    ConfiguracionSerializacionProgramacion.ConfigurarResolver(resolver);
                    jsonOptions.TypeInfoResolver = resolver;
                });

                // Async es el lifecycle canonico del worker (MEF-ADR-0034 seccion 3). Cada documento
                // destino debe llevar [MaterializadoPorProyeccion] (ver su porque alli).
                opts.Projections.Add<FichaTurnoProjection>(ProjectionLifecycle.Async);
                opts.Projections.Add<CuadroDeTurnosProjection>(ProjectionLifecycle.Async);
                opts.Projections.Add<AusenciaVigenteProjection>(ProjectionLifecycle.Async);
                opts.Projections.Add<LimitesDeJornadaProjection>(ProjectionLifecycle.Async);
                opts.Projections.Add<BorradorDeProgramacionProjection>(ProjectionLifecycle.Async);
            })
            // Sin esta llamada el daemon queda apagado. HotCold elige lider con advisory locks: Azure
            // puede correr el Container App momentaneamente con mas de una replica.
            .AddAsyncDaemon(DaemonMode.HotCold);

        return services;
    }
}
