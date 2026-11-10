using System.Text.Json.Serialization.Metadata;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Projections.Colaboradores;
using Bitakora.ControlAsistencia.ReadModels.Colaboradores;
using JasperFx.Events; // StreamIdentity, EventNamingStyle (NO Marten.Events, mismo gotcha que DaemonMode)
using JasperFx.Events.Daemon; // DaemonMode (NO Marten.Events.Daemon: compila pero deja DaemonMode sin resolver)
using JasperFx.Events.Projections; // ProjectionLifecycle (NO Marten.Events.Projections)
using JasperFx.MultiTenancy; // TenancyStyle (NO Marten.*, mismo gotcha que StreamIdentity/DaemonMode)
using Marten;
using Weasel.Core; // EnumStorage, Casing (NO Marten.*: viven en Weasel.Core)
using Weasel.Postgresql.Tables; // IndexMethod (NO Marten.*: vive en Weasel.Postgresql.Tables)

namespace Bitakora.ControlAsistencia.Projections.Infraestructura;

/// <summary>
/// Marker del named store de proyecciones del dominio Colaboradores (MEF-ADR-0034 seccion 2).
/// </summary>
public interface IColaboradoresProjectionStore : IDocumentStore;

/// <summary>
/// Seam de proyecciones de Colaboradores. El porque de cada linea que replica el write-side esta en
/// <see cref="ConfiguracionMartenProjectionsProgramacion"/>.
/// </summary>
public static class ConfiguracionMartenProjectionsColaboradores
{
    private const string SchemaDelDominio = "colaboradores";

    public static IServiceCollection ConfigurarColaboradores(
        this IServiceCollection services, string martenConnectionString)
    {
        services.AddMartenStore<IColaboradoresProjectionStore>(opts =>
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

                opts.Events.AddEventTypes(IdentidadEventosColaboradores.TiposPersistidos);

                // Identificacion y NombreColaborador son VOs con campos privados: sin este resolver el
                // daemon reventaria con NotSupportedException en runtime, no en el build.
                opts.UseSystemTextJsonForSerialization(EnumStorage.AsInteger, Casing.Default, jsonOptions =>
                {
                    var resolver = new DefaultJsonTypeInfoResolver();
                    ConfiguracionSerializacionColaboradores.ConfigurarResolver(resolver);
                    jsonOptions.TypeInfoResolver = resolver;
                });

                opts.Projections.Add<FichaColaboradorProjection>(ProjectionLifecycle.Async);

                opts.Projections.Add<CategoriaDeEtiquetasProjection>(ProjectionLifecycle.Async);

                // Indices del listado ListarFichasColaborador. El GIN se crea sobre la expresion
                // (data->>'EtiquetasNormalizadas')::jsonb: el endpoint reproduce ese MISMO shape via
                // MatchesSql para que el planner elija el Bitmap Index Scan (verificado con EXPLAIN,
                // Marten 9.12.0 + Postgres 16); si cambias uno, cambia el otro. VigenteHasta: filtro
                // de vigencia de toda consulta. NombreCompleto: ORDER BY del keyset.
                opts.Schema.For<FichaColaborador>()
                    .Index(x => x.EtiquetasNormalizadas, i => i.Method = IndexMethod.gin)
                    .Index(x => x.VigenteHasta)
                    .Index(x => x.NombreCompleto);

                opts.Projections.Add<DirectorioColaboradorProjection>(ProjectionLifecycle.Async);

                // Indices del QUERY del directorio: NumeroDocumento (numero suelto), GIN sobre
                // TokensNombre (containment de todos los tokens) y NombreCompleto (ORDER BY). Sin
                // verificar con EXPLAIN: llega con el endpoint que los consuma.
                opts.Schema.For<DirectorioColaborador>()
                    .Index(x => x.NumeroDocumento)
                    .Index(x => x.TokensNombre, i => i.Method = IndexMethod.gin)
                    .Index(x => x.NombreCompleto);
            })
            .AddAsyncDaemon(DaemonMode.HotCold);

        return services;
    }
}
