using Microsoft.Extensions.Configuration;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;

// Vive aparte para que ApiFixture y ServiceBusFixture declaren la misma identidad de tenant.
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
