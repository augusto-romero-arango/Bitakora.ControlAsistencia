using Microsoft.Extensions.DependencyInjection;

namespace Bitakora.ControlAsistencia.Projections.Infraestructura;

/// <summary>
/// Seam base de composicion del worker de proyecciones (MEF-ADR-0034). Program.cs solo invoca
/// este metodo -- no wirea nada inline.
/// </summary>
public static class ConfiguracionMartenProjections
{
    public static IServiceCollection ConfigurarEventos(this IServiceCollection services, string martenConnectionString)
    {
        // Extension point (issue #370): cada dominio que adopte proyecciones contribuye su
        // propio ConfiguracionMartenProjections{Dominio}.Configurar{Dominio}(services,
        // martenConnectionString) (MEF-ADR-0006/MEF-ADR-0034 seccion 2) -- domain-scaffolder
        // invoca ese metodo aqui, uno por dominio. Sin dominios adoptados todavia (issue #367),
        // este seam no registra ningun named store.

        return services;
    }
}
