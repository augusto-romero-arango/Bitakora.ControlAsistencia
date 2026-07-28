using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace Bitakora.ControlAsistencia.Projections.Infraestructura;

/// <summary>
/// Marker del named store de proyecciones del dominio ControlHoras (MEF-ADR-0034 seccion 2).
/// </summary>
public interface IControlHorasProjectionStore : IDocumentStore;

/// <summary>
/// Seam de composicion de proyecciones del dominio ControlHoras (MEF-ADR-0006/MEF-ADR-0034
/// seccion 2 y 6) -- hermano read-side de ComposicionServicios (write-side, MEF-ADR-0029).
///
/// Fase roja (issue #235, projection-test-writer): a diferencia del seam que domain-scaffolder
/// emite ya implementado en su Paso 3b, este archivo no existia todavia -- ControlHoras nacio
/// antes de que el BC adoptara proyecciones (issue #370). El metodo se declara partial con
/// modificadores de acceso (el config-test lo invoca desde otro ensamblado, asi que necesita
/// ser alcanzable) -- eso obliga al compilador a exigir la parte implementadora (CS8795), que
/// aqui es el stub estandar de fase roja. La implementacion real (named store con
/// DatabaseSchemaName = "control_horas", replica de Events.MetadataConfig y
/// AddAsyncDaemon(DaemonMode.HotCold)) es alcance de projection-implementer.
/// </summary>
public static partial class ConfiguracionMartenProjectionsControlHoras
{
    public static partial IServiceCollection ConfigurarControlHoras(
        this IServiceCollection services, string martenConnectionString);
}

public static partial class ConfiguracionMartenProjectionsControlHoras
{
    public static partial IServiceCollection ConfigurarControlHoras(
        this IServiceCollection services, string martenConnectionString)
    {
        throw new NotImplementedException();
    }
}
