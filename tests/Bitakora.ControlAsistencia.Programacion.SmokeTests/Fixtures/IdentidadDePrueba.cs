using Microsoft.Extensions.Configuration;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

// Issue #538: identidad de tenant que los fixtures declaran contra dev. La etapa (b) de tenancy
// (MEF-ADR-0028 seccion 4) la exige en cada activacion; en la etapa (a) vigente (TenantResolverFijo)
// se ignora, asi que declararla hoy es inocuo y evita una ventana rota en el flip.
// Vive aparte de los fixtures para que los dos planos -- HTTP (ApiFixture) y Service Bus
// (ServiceBusFixture) -- declaren la MISMA identidad sin poder desincronizarse.
internal sealed record IdentidadDePrueba(string TenantId, string UserId)
{
    private const string TenantIdPorDefecto = "tenant-smoke";
    private const string UserIdPorDefecto = "smoke@bitakora.dev";

    public static IdentidadDePrueba Desde(IConfiguration configuration) => new(
        Configurado(configuration["Tenant:Id"], TenantIdPorDefecto),
        Configurado(configuration["Tenant:UserId"], UserIdPorDefecto));

    // Los appsettings de smoke usan cadena vacia como "sin configurar" (ver ServiceBus:ConnectionString),
    // asi que el fallback mira blancos y no solo null.
    private static string Configurado(string? valor, string porDefecto) =>
        string.IsNullOrWhiteSpace(valor) ? porDefecto : valor;
}
