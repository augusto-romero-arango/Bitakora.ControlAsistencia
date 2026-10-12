namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

// CA-ADR-0040: purga por contenido antes del Act y borrado al final, aunque el test falle. La clase de
// test lo declara como campo y lo dispone desde su propio DisposeAsync.
public sealed class LimpiezaDeJornadas(PostgresFixture postgres) : IAsyncDisposable
{
    private readonly List<FirmaDeLimites> _firmas = [];
    private readonly List<string> _ids = [];

    public async Task PrepararAsync(params FirmaDeLimites[] firmas)
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");
        foreach (var firma in firmas)
        {
            await postgres.PurgarJornadasPorFirmaAsync(
                PostgresFixture.SchemaProgramacion, postgres.TenantId, firma);
            _firmas.Add(firma);
        }
    }

    public void Registrar(Guid jornadaId) => _ids.Add(jornadaId.ToString());

    public async ValueTask DisposeAsync()
    {
        if (!postgres.IsConfigured) return;
        foreach (var firma in _firmas)
            await postgres.PurgarJornadasPorFirmaAsync(
                PostgresFixture.SchemaProgramacion, postgres.TenantId, firma);
        if (_ids.Count > 0)
            await postgres.BorrarStreamsAsync(PostgresFixture.SchemaProgramacion, postgres.TenantId, _ids);
    }
}
