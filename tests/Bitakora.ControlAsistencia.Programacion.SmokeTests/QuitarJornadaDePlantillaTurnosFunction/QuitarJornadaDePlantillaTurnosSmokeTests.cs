using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.QuitarJornadaDePlantillaTurnosFunction;

public class QuitarJornadaDePlantillaTurnosSmokeTests(ApiFixture api, PostgresFixture postgres)
    : IDisposable
{
    private readonly TenantDeTest _tenant = new(api);

    public void Dispose() => _tenant.Dispose();

    private const string TipoEventoQuitada = "jornada_de_plantilla_de_turnos_quitada";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private HttpClient _client => _tenant.Client;

    private static string Ruta(Guid plantillaId) =>
        $"/api/programacion/plantillas-turnos/{plantillaId}/jornada";

    private async Task<Guid> CrearPlantillaConJornadaAsync(FirmaDeLimites firma, CancellationToken ct)
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
            .StatusCode.Should().Be(HttpStatusCode.Created, "el arrange depende de que CrearJornada funcione");
        (await _client.PutAsJsonAsync(Ruta(plantillaId), new { jornadaId }, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent,
                "el arrange depende de que AsignarJornadaAPlantillaTurnos funcione");

        return plantillaId;
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarJornada_DebeRetornar204YPersistirElEvento_CuandoLaPlantillaTieneJornada()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");
        var ct = TestContext.Current.CancellationToken;
        var plantillaId = await CrearPlantillaConJornadaAsync(
            LimitesDePrueba.QuitarJornadaDePlantillaTurnosPersiste, ct);

        var response = await _client.DeleteAsync(Ruta(plantillaId), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var existe = await postgres.ExisteEventoAsync(
            PostgresFixture.SchemaProgramacion, plantillaId.ToString(), TipoEventoQuitada, Timeout);
        existe.Should().BeTrue($"el evento {TipoEventoQuitada} deberia existir en el stream {plantillaId}");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarJornada_DebeRetornar204SinNuevosEventos_CuandoLaPlantillaNoTieneJornada()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");
        var ct = TestContext.Current.CancellationToken;
        var plantillaId = await CrearPlantillaConJornadaAsync(
            LimitesDePrueba.QuitarJornadaDePlantillaTurnosSinNuevosEventos, ct);
        (await _client.DeleteAsync(Ruta(plantillaId), ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var streamId = plantillaId.ToString();
        var eventosTrasPrimera = await postgres.ContarEventosDeStreamAsync(
            PostgresFixture.SchemaProgramacion, _tenant.TenantId, streamId);

        var response = await _client.DeleteAsync(Ruta(plantillaId), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var eventosTrasSegunda = await postgres.ContarEventosDeStreamAsync(
            PostgresFixture.SchemaProgramacion, _tenant.TenantId, streamId);
        eventosTrasSegunda.Should().Be(eventosTrasPrimera);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarJornada_DebeRetornar404_CuandoLaPlantillaNoExiste()
    {
        var response = await _client.DeleteAsync(
            Ruta(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
