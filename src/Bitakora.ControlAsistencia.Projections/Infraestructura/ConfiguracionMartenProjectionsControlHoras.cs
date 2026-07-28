using JasperFx.Events.Daemon; // DaemonMode (NO Marten.Events.Daemon: compila pero deja DaemonMode sin resolver)
using Marten;

namespace Bitakora.ControlAsistencia.Projections.Infraestructura;

/// <summary>
/// Marker del named store de proyecciones del dominio ControlHoras (MEF-ADR-0034 seccion 2).
/// </summary>
public interface IControlHorasProjectionStore : IDocumentStore;

/// <summary>
/// Seam de composicion de proyecciones del dominio ControlHoras (MEF-ADR-0006/MEF-ADR-0034
/// secciones 2 y 6) -- hermano read-side de ComposicionServicios (write-side, MEF-ADR-0029):
/// fuente unica que comparten Program.cs del worker y el config-test.
///
/// Registra el named store sobre la misma conexion y el mismo schema "control_horas" que ya usa
/// el write-side (ComposicionServicios.AgregarServiciosControlHoras) -- el read-side no crea
/// base ni schema nuevos, solo re-declara del lado lectura lo que el dominio ya posee del lado
/// escritura. Sin ninguna proyeccion concreta todavia: las agrega projection-implementer sobre
/// este mismo seam, siempre con lifecycle Async (MEF-ADR-0034 seccion 3).
///
/// El seam se declara con modificadores de acceso y sin partial: un metodo partial sin
/// modificadores desaparece en silencio al compilar si nadie lo implementa, y ademas seria
/// implicitamente privado e inalcanzable desde el ensamblado de tests.
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

                // Replica de la configuracion de metadata del write-side (MEF-ADR-0034 seccion 6
                // punto 3, seccion 7): el config-test verifica exactamente estas tres. La
                // habilitacion real de las columnas es responsabilidad del write-side de este
                // dominio (issue #232).
                opts.Events.MetadataConfig.CorrelationIdEnabled = true;
                opts.Events.MetadataConfig.CausationIdEnabled = true;
                opts.Events.MetadataConfig.HeadersEnabled = true;
            })
            // Registrar el store no basta: sin esta llamada el daemon queda apagado y ninguna
            // proyeccion se materializa. HotCold elige lider sobre advisory locks de PostgreSQL,
            // lo correcto para un Container App que Azure puede correr momentaneamente con mas
            // de una replica (MEF-ADR-0034 seccion 2).
            .AddAsyncDaemon(DaemonMode.HotCold);

        return services;
    }
}
