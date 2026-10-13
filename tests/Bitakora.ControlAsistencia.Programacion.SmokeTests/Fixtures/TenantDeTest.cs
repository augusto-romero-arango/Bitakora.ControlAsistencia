namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

// CA-ADR-0040: tenant propio por test; nunca se borra, el residuo queda aislado por tenant.
public sealed class TenantDeTest : IDisposable
{
    public const string Prefijo = "tenant-smoke-";

    public TenantDeTest(ApiFixture api)
    {
        TenantId = $"{Prefijo}{Guid.NewGuid():N}";
        Client = api.CrearClienteParaTenant(TenantId);
    }

    public string TenantId { get; }

    public HttpClient Client { get; }

    public void Dispose() => Client.Dispose();
}
