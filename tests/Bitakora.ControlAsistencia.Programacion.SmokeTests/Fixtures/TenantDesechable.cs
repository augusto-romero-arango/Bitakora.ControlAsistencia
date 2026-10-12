namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

// CA-ADR-0040: tenant propio para un test que necesita ver vacio un singleton por tenant. Al disponerse
// borra por Postgres todo lo del tenant, aunque el test falle.
public sealed class TenantDesechable : IAsyncDisposable
{
    private readonly PostgresFixture _postgres;

    public TenantDesechable(ApiFixture api, PostgresFixture postgres)
    {
        _postgres = postgres;
        TenantId = $"{PostgresFixture.PrefijoTenantDesechable}{Guid.NewGuid():N}";
        Client = api.CrearClienteParaTenant(TenantId);
    }

    public string TenantId { get; }

    public HttpClient Client { get; }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_postgres.IsConfigured)
                await _postgres.BorrarTenantAsync(PostgresFixture.SchemaProgramacion, TenantId);
        }
        finally
        {
            Client.Dispose();
        }
    }
}
