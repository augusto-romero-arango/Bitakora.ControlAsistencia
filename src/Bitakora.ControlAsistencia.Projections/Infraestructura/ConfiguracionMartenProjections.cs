namespace Bitakora.ControlAsistencia.Projections.Infraestructura;

/// <summary>
/// Seam base de composicion del worker de proyecciones (MEF-ADR-0034). Program.cs solo invoca
/// este metodo -- no wirea nada inline.
/// </summary>
public static class ConfiguracionMartenProjections
{
    public static IServiceCollection ConfigurarEventos(this IServiceCollection services, string martenConnectionString)
    {
        // Aditivo: cada dominio que adopte proyecciones agrega aqui su Configurar{Dominio}.
        services.ConfigurarProgramacion(martenConnectionString);
        services.ConfigurarControlHoras(martenConnectionString);
        services.ConfigurarColaboradores(martenConnectionString);
        services.ConfigurarSedes(martenConnectionString);

        return services;
    }
}
