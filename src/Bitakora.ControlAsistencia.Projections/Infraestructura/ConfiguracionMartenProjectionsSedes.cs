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
/// Seam de proyecciones de Sedes. El porque de cada linea que replica el write-side esta en
/// <see cref="ConfiguracionMartenProjectionsProgramacion"/>.
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

                opts.Events.StreamIdentity = StreamIdentity.AsString;

                opts.Events.TenancyStyle = TenancyStyle.Conjoined;

                opts.Events.EventNamingStyle = EventNamingStyle.SmarterTypeName;

                opts.Policies.AllDocumentsAreMultiTenanted();

                opts.TolerarHuecosEstancados();

                opts.Events.MetadataConfig.CorrelationIdEnabled = true;
                opts.Events.MetadataConfig.CausationIdEnabled = true;
                opts.Events.MetadataConfig.HeadersEnabled = true;

                opts.Events.AddEventTypes(IdentidadEventosSedes.TiposPersistidos);

                opts.UseSystemTextJsonForSerialization(EnumStorage.AsInteger, Casing.Default, jsonOptions =>
                {
                    var resolver = new DefaultJsonTypeInfoResolver();
                    ConfiguracionSerializacionSedes.ConfigurarResolver(resolver);
                    jsonOptions.TypeInfoResolver = resolver;
                });

                opts.Projections.Add<FichaSedeProjection>(ProjectionLifecycle.Async);

                // N2: el mismo dispositivo puede estar instalado en streams de VARIAS sedes, asi que
                // correlaciona por DispositivoId, no por stream.
                opts.Projections.Add<UbicacionDispositivoProjection>(ProjectionLifecycle.Async);
            })
            .AddAsyncDaemon(DaemonMode.HotCold);

        return services;
    }
}
