using System.Net.Http.Json;
using System.Text.Json;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

// CA-ADR-0040: purga por contenido antes del Act y borrado al final, aunque el test falle. La clase de
// test lo declara como campo y lo dispone desde su propio DisposeAsync.
public sealed class LimpiezaDeJornadas(PostgresFixture postgres) : IAsyncDisposable
{
    private const string RutaPredeterminada = "/api/programacion/preferencias/jornada-predeterminada";

    private readonly List<FirmaDeLimites> _firmas = [];
    private readonly List<string> _ids = [];
    private HttpClient? _clientePredeterminada;
    private Guid? _predeterminadaPrevia;

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

    // Llamar antes de volver predeterminada una Jornada del test: al borrarla, Preferencias quedaria
    // apuntando a un stream inexistente y SolicitarProgramacion sin jornada responderia 404.
    public async Task RecordarPredeterminadaAsync(HttpClient client)
    {
        _clientePredeterminada = client;
        var asignaciones = await postgres.LeerEventosDeStreamAsync(
            PostgresFixture.SchemaProgramacion, postgres.TenantId, StreamPreferencias);
        _predeterminadaPrevia = asignaciones.Count == 0 ? null : JornadaId(asignaciones[^1]);
    }

    public async ValueTask DisposeAsync()
    {
        if (!postgres.IsConfigured) return;
        try
        {
            await RestaurarPredeterminadaAsync();
        }
        finally
        {
            foreach (var firma in _firmas)
                await postgres.PurgarJornadasPorFirmaAsync(
                    PostgresFixture.SchemaProgramacion, postgres.TenantId, firma);
            if (_ids.Count > 0)
                await postgres.BorrarStreamsAsync(PostgresFixture.SchemaProgramacion, postgres.TenantId, _ids);
        }
    }

    private string StreamPreferencias => PostgresFixture.StreamIdPreferencias(postgres.TenantId);

    // Sin predeterminada previa restaurable se borra Preferencias: el asegurador la recrea al primer uso.
    private async Task RestaurarPredeterminadaAsync()
    {
        if (_clientePredeterminada is null) return;
        if (_predeterminadaPrevia is { } previa)
        {
            var restaurada = await _clientePredeterminada.PutAsJsonAsync(
                RutaPredeterminada, new { jornadaId = previa });
            if (restaurada.IsSuccessStatusCode) return;
        }

        await postgres.BorrarStreamsAsync(
            PostgresFixture.SchemaProgramacion, postgres.TenantId, [StreamPreferencias]);
    }

    private static Guid? JornadaId(JsonElement asignacion) =>
        asignacion.TryGetProperty("jornadaId", out var id) || asignacion.TryGetProperty("JornadaId", out id)
            ? id.GetGuid()
            : null;
}
