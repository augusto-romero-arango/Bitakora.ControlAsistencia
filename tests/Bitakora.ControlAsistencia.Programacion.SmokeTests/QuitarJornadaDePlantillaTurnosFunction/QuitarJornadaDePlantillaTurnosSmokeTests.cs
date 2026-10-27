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

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarJornada_DebeRetornar204YPersistirElEvento_CuandoLaPlantillaTieneJornada()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");
        var ct = TestContext.Current.CancellationToken;
        var plantillaId = Guid.CreateVersion7();
        var jornadaId = Guid.CreateVersion7();
        (await _client.PostAsJsonAsync("/api/programacion/plantillas-turnos", new
        {
            plantillaId,
            nombre = $"[TEST] Plantilla {plantillaId}",
            diasDelCiclo = 7
        }, ct)).StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange depende de que CrearPlantillaTurnos funcione");
        (await _client.PostAsJsonAsync("/api/programacion/jornadas",
                LimitesDePrueba.QuitarJornadaDePlantillaTurnosPersiste.PayloadCrear(jornadaId), ct))
            .StatusCode.Should().Be(HttpStatusCode.Created, "el arrange depende de que CrearJornada funcione");
        (await _client.PutAsJsonAsync(Ruta(plantillaId), new { jornadaId }, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent,
                "el arrange depende de que AsignarJornadaAPlantillaTurnos funcione");

        var response = await _client.DeleteAsync(Ruta(plantillaId), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var existe = await postgres.ExisteEventoAsync(
            PostgresFixture.SchemaProgramacion, plantillaId.ToString(), TipoEventoQuitada, Timeout);
        existe.Should().BeTrue($"el evento {TipoEventoQuitada} deberia existir en el stream {plantillaId}");
    }
}
