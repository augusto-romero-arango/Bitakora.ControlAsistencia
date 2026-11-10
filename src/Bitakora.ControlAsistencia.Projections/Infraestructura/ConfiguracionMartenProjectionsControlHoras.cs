using System.Text.Json.Serialization.Metadata;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.Projections.ControlHoras;
using JasperFx.Events; // StreamIdentity, EventNamingStyle (NO Marten.Events, mismo gotcha que DaemonMode)
using JasperFx.Events.Daemon; // DaemonMode (NO Marten.Events.Daemon: compila pero deja DaemonMode sin resolver)
using JasperFx.Events.Projections; // ProjectionLifecycle (NO Marten.Events.Projections)
using JasperFx.MultiTenancy; // TenancyStyle (NO Marten.*, mismo gotcha que StreamIdentity/DaemonMode)
using Marten;
using Weasel.Core; // EnumStorage, Casing (NO Marten.*: viven en Weasel.Core)

namespace Bitakora.ControlAsistencia.Projections.Infraestructura;

/// <summary>
/// Marker del named store de proyecciones del dominio ControlHoras (MEF-ADR-0034 seccion 2).
/// </summary>
public interface IControlHorasProjectionStore : IDocumentStore;

/// <summary>
/// Seam de proyecciones de ControlHoras. El porque de cada linea que replica el write-side esta en
/// <see cref="ConfiguracionMartenProjectionsProgramacion"/>.
/// </summary>
public static class ConfiguracionMartenProjectionsControlHoras
{
    private const string SchemaDelDominio = "control_horas";

    public static IServiceCollection ConfigurarControlHoras(
        this IServiceCollection services, string martenConnectionString)
    {
        services.AddMartenStore<IControlHorasProjectionStore>(opts =>
            {
                opts.Connection(martenConnectionString);
                opts.DatabaseSchemaName = SchemaDelDominio; // mismo schema que el write-side (MEF-ADR-0003)

                opts.Events.StreamIdentity = StreamIdentity.AsString;

                opts.Events.TenancyStyle = TenancyStyle.Conjoined;

                opts.Events.EventNamingStyle = EventNamingStyle.SmarterTypeName;

                opts.Policies.AllDocumentsAreMultiTenanted();

                opts.TolerarHuecosEstancados();

                opts.Events.MetadataConfig.CorrelationIdEnabled = true;
                opts.Events.MetadataConfig.CausationIdEnabled = true;
                opts.Events.MetadataConfig.HeadersEnabled = true;

                opts.Events.AddEventTypes(IdentidadEventosControlHoras.TiposPersistidos);

                // Solo ConfiguracionSerializacionControlHoras: ConfiguracionSerializacionCalculoHoras
                // vive en el Function App, cubre VOs de calculo que no se persisten y este ensamblado
                // no puede referenciarla (CA-ADR-0029).
                opts.UseSystemTextJsonForSerialization(EnumStorage.AsInteger, Casing.Default, jsonOptions =>
                {
                    var resolver = new DefaultJsonTypeInfoResolver();
                    ConfiguracionSerializacionControlHoras.ConfigurarResolver(resolver);
                    jsonOptions.TypeInfoResolver = resolver;
                });

                opts.Projections.Add<TurnoVigenteProjection>(ProjectionLifecycle.Async);
                opts.Projections.Add<AsistenciaDiariaProjection>(ProjectionLifecycle.Async);
                opts.Projections.Add<AdvertenciasProgramacionSemanalProjection>(ProjectionLifecycle.Async);
            })
            .AddAsyncDaemon(DaemonMode.HotCold);

        return services;
    }
}
