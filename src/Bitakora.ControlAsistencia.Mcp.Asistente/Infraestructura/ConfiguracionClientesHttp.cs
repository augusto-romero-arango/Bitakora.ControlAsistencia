using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

/// <summary>
/// Seam de composicion de los HttpClients tipados del servidor (MEF-ADR-0029, MEF-ADR-0047
/// decision 3). Cada base URL se lee aqui, durante la composicion del host, y no dentro del
/// delegate de AddHttpClient: ese delegate no corre hasta que alguien resuelve el cliente
/// tipado, asi que un app setting Api__{Dominio}__BaseUrl ausente fallaria recien en la primera
/// tool call. Leerla afuera mueve el fallo al ARRANQUE, que es lo que este seam promete.
/// </summary>
public static class ConfiguracionClientesHttp
{
    public static IServiceCollection ConfigurarClientesHttp(this IServiceCollection services, IConfiguration configuration)
    {
        var baseUrlProgramacion = LeerBaseUrl(configuration, "Programacion");
        services.AddHttpClient<ProgramacionApi>(c => c.BaseAddress = baseUrlProgramacion)
            .AddHttpMessageHandler<PropagadorIdentidadTenantHandler>();

        var baseUrlSedes = LeerBaseUrl(configuration, "Sedes");
        services.AddHttpClient<SedesApi>(c => c.BaseAddress = baseUrlSedes)
            .AddHttpMessageHandler<PropagadorIdentidadTenantHandler>();

        var baseUrlColaboradores = LeerBaseUrl(configuration, "Colaboradores");
        services.AddHttpClient<ColaboradoresApi>(c => c.BaseAddress = baseUrlColaboradores)
            .AddHttpMessageHandler<PropagadorIdentidadTenantHandler>();

        var baseUrlControlHoras = LeerBaseUrl(configuration, "ControlHoras");
        services.AddHttpClient<ControlHorasApi>(c => c.BaseAddress = baseUrlControlHoras)
            .AddHttpMessageHandler<PropagadorIdentidadTenantHandler>();

        return services;
    }

    private static Uri LeerBaseUrl(IConfiguration configuration, string dominio)
    {
        var clave = $"Api:{dominio}:BaseUrl";
        var valor = configuration[clave];
        return string.IsNullOrWhiteSpace(valor)
            ? throw new InvalidOperationException($"Falta el app setting Api__{dominio}__BaseUrl")
            : new Uri(valor);
    }
}
