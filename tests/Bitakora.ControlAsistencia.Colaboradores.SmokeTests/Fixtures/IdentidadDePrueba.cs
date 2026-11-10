using Microsoft.Extensions.Configuration;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;

// Identidad de tenant que los fixtures declaran contra dev y TenantContextMiddleware lee en cada
// activacion. Vive aparte para que HTTP (ApiFixture) y Service Bus (ServiceBusFixture) declaren la
// misma identidad sin desincronizarse.
internal sealed record IdentidadDePrueba(string TenantId, string UserId, string OrganizationMembershipId)
{
    private const string TenantIdPorDefecto = "tenant-smoke";
    private const string UserIdPorDefecto = "smoke@bitakora.dev";
    private const string OrganizationMembershipIdPorDefecto = "om-smoke";

    public static IdentidadDePrueba Desde(IConfiguration configuration) => new(
        Configurado(configuration["Tenant:Id"], TenantIdPorDefecto),
        Configurado(configuration["Tenant:UserId"], UserIdPorDefecto),
        Configurado(configuration["Tenant:OrganizationMembershipId"], OrganizationMembershipIdPorDefecto));

    // Los appsettings de smoke usan cadena vacia como "sin configurar" (ver ServiceBus:ConnectionString),
    // asi que el fallback mira blancos y no solo null.
    private static string Configurado(string? valor, string porDefecto) =>
        string.IsNullOrWhiteSpace(valor) ? porDefecto : valor;
}
