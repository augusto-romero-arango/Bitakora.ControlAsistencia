namespace Bitakora.ControlAsistencia.Projections.Infraestructura;

/// <summary>
/// Seam base de composicion del worker de proyecciones (MEF-ADR-0034). Program.cs solo invoca
/// este metodo -- no wirea nada inline.
/// </summary>
public static class ConfiguracionMartenProjections
{
    public static IServiceCollection ConfigurarEventos(this IServiceCollection services, string martenConnectionString)
    {
        // Issue #235: Programacion y ControlHoras nacieron antes de que el BC adoptara
        // proyecciones (issue #370), asi que su registro se agrega aqui explicitamente en vez de
        // via domain-scaffolder. Cada dominio nuevo que adopte proyecciones sigue el mismo patron
        // aditivo: agrega su llamada a services.Configurar{Dominio}(martenConnectionString) sin
        // remover las anteriores (MEF-ADR-0006/MEF-ADR-0034 seccion 2).
        services.ConfigurarProgramacion(martenConnectionString);
        services.ConfigurarControlHoras(martenConnectionString);
        services.ConfigurarColaboradores(martenConnectionString);
        services.ConfigurarSedes(martenConnectionString);

        return services;
    }
}
