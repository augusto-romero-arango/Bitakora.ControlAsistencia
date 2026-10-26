using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public static class ConfiguracionServiciosMcp
{
    public static IServiceCollection ConfigurarServiciosMcp(this IServiceCollection services, IConfiguration configuration)
    {
        services.ConfigurarIdentidadTenant(configuration);
        services.ConfigurarClientesHttp(configuration);
        services.AddSingleton(TimeProvider.System);

        // Defensa en profundidad (MEF-ADR-0047 decision 7): el gate real vive en la politica dedicada de
        // APIM (MEF-ADR-0032 seccion 9). ValidateAudience = false -- la audiencia ya la exige esa politica.
        // Sin Mcp__AuthorizationServer resoluble el validador degrada a "todo token es invalido"; no
        // fail-fast de arranque, a diferencia de las base URLs de los clientes tipados.
        services.AddSingleton<IValidadorTokenAuthKit>(
            ValidadorTokenAuthKit.ParaAuthorizationServer(configuration["Mcp:AuthorizationServer"]));

        // Transient: envuelven un cliente tipado, que no debe capturarse en un singleton.
        services.AddTransient<ResolutorTurnoPorNombre>();
        services.AddTransient<ResolutorTurnoPorId>();
        services.AddTransient<ResolutorPlantillaPorNombre>();
        services.AddTransient<ResolutorSedePorCodigo>();
        services.AddTransient<ResolutorCandidatosPorLista>();
        services.AddTransient<ResolutorCandidatosPorGrupo>();

        return services;
    }
}
