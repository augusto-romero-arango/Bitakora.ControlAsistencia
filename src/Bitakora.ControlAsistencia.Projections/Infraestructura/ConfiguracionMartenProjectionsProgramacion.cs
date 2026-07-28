using JasperFx.Events.Daemon; // DaemonMode (NO Marten.Events.Daemon: compila pero deja DaemonMode sin resolver)
using Marten;

namespace Bitakora.ControlAsistencia.Projections.Infraestructura;

/// <summary>
/// Marker del named store de proyecciones del dominio Programacion (MEF-ADR-0034 seccion 2).
/// </summary>
public interface IProgramacionProjectionStore : IDocumentStore;

/// <summary>
/// Seam de composicion de proyecciones del dominio Programacion (MEF-ADR-0006/MEF-ADR-0034
/// seccion 2 y 6) -- hermano read-side de ComposicionServicios (write-side, MEF-ADR-0029).
///
/// Fase verde (issue #235, projection-implementer): registra el named store de Marten sobre
/// el mismo schema "programacion" que ya usa el write-side
/// (ComposicionServicios.AgregarServiciosProgramacion), replicando la configuracion de
/// metadata de evento que el write-side habilito en el issue #232, y con el daemon en modo
/// HotCold (eleccion de lider sobre advisory locks, correcto para un Container App que Azure
/// puede correr momentaneamente con mas de una replica). Sin ninguna proyeccion concreta
/// todavia -- las agrega projection-implementer sobre este mismo seam en issues tipo:projection
/// posteriores. El metodo conserva la forma partial con modificadores de acceso que dejo
/// projection-test-writer (issue #235): el compilador ya cubre la guarda 1 del config-test
/// (CS8795 exige esta parte implementadora), y el test conserva valor por las guardas 2 y 3.
/// </summary>
public static partial class ConfiguracionMartenProjectionsProgramacion
{
    public static partial IServiceCollection ConfigurarProgramacion(
        this IServiceCollection services, string martenConnectionString);
}

public static partial class ConfiguracionMartenProjectionsProgramacion
{
    public static partial IServiceCollection ConfigurarProgramacion(
        this IServiceCollection services, string martenConnectionString)
    {
        services.AddMartenStore<IProgramacionProjectionStore>(opts =>
            {
                opts.Connection(martenConnectionString);
                opts.DatabaseSchemaName = "programacion"; // mismo schema que el write-side (MEF-ADR-0003)

                // Replica de la configuracion de metadata del write-side (MEF-ADR-0034 seccion 6
                // punto 3, seccion 7): el config-test verifica exactamente estas tres. La
                // habilitacion real de las columnas es responsabilidad del write-side de este
                // dominio (issue #232).
                opts.Events.MetadataConfig.CorrelationIdEnabled = true;
                opts.Events.MetadataConfig.CausationIdEnabled = true;
                opts.Events.MetadataConfig.HeadersEnabled = true;
            })
            .AddAsyncDaemon(DaemonMode.HotCold);

        return services;
    }
}
