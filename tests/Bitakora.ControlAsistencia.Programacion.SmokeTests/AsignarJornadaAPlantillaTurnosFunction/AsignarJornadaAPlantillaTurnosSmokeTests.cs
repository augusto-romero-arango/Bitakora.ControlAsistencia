using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.AsignarJornadaAPlantillaTurnosFunction;

public class AsignarJornadaAPlantillaTurnosSmokeTests(ApiFixture api, PostgresFixture postgres)
    : IDisposable
{
    private readonly TenantDeTest _tenant = new(api);

    public void Dispose() => _tenant.Dispose();

    private const string TipoEventoAsignada = "jornada_de_plantilla_de_turnos_asignada";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private HttpClient _client => _tenant.Client;

    private static string Ruta(Guid plantillaId) =>
        $"/api/programacion/plantillas-turnos/{plantillaId}/jornada";

    private async Task<(Guid PlantillaId, Guid JornadaId)> CrearPlantillaYJornadaAsync(
        FirmaDeLimites firma, CancellationToken ct)
    {
        var plantillaId = Guid.CreateVersion7();
        var jornadaId = Guid.CreateVersion7();

        (await _client.PostAsJsonAsync("/api/programacion/plantillas-turnos", new
        {
            plantillaId,
            nombre = $"[TEST] Plantilla {plantillaId}",
            diasDelCiclo = 7
        }, ct)).StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange depende de que CrearPlantillaTurnos funcione");
        (await _client.PostAsJsonAsync("/api/programacion/jornadas", firma.PayloadCrear(jornadaId), ct))
            .StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange depende de que CrearJornada funcione");

        return (plantillaId, jornadaId);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarJornada_DebeRetornar204YPersistirElEvento_CuandoLaJornadaExiste()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");
        var ct = TestContext.Current.CancellationToken;
        var (plantillaId, jornadaId) = await CrearPlantillaYJornadaAsync(
            LimitesDePrueba.AsignarAPlantillaTurnosPersiste, ct);

        var response = await _client.PutAsJsonAsync(Ruta(plantillaId), new { jornadaId }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var existe = await postgres.ExisteEventoAsync(
            PostgresFixture.SchemaProgramacion, plantillaId.ToString(), TipoEventoAsignada, Timeout);
        existe.Should().BeTrue($"el evento {TipoEventoAsignada} deberia existir en el stream {plantillaId}");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarJornada_DebeRetornar204SinNuevosEventos_CuandoSeRepiteConLaCopiaAlDia()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");
        var ct = TestContext.Current.CancellationToken;
        var (plantillaId, jornadaId) = await CrearPlantillaYJornadaAsync(
            LimitesDePrueba.AsignarAPlantillaTurnosRepite, ct);
        var streamId = plantillaId.ToString();

        (await _client.PutAsJsonAsync(Ruta(plantillaId), new { jornadaId }, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var eventosTrasPrimera = await postgres.ContarEventosDeStreamAsync(
            PostgresFixture.SchemaProgramacion, _tenant.TenantId, streamId);

        var response = await _client.PutAsJsonAsync(Ruta(plantillaId), new { jornadaId }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var eventosTrasSegunda = await postgres.ContarEventosDeStreamAsync(
            PostgresFixture.SchemaProgramacion, _tenant.TenantId, streamId);
        eventosTrasSegunda.Should().Be(eventosTrasPrimera);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarJornada_DebeRetornar404_CuandoLaJornadaNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var (plantillaId, _) = await CrearPlantillaYJornadaAsync(
            LimitesDePrueba.AsignarAPlantillaTurnosJornadaNoExiste, ct);

        var response = await _client.PutAsJsonAsync(
            Ruta(plantillaId), new { jornadaId = Guid.CreateVersion7() }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
