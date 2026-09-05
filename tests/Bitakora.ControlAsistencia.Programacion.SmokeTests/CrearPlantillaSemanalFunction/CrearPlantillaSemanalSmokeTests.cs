using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.CrearPlantillaSemanalFunction;

// Issue #620: primer endpoint del BC con el codigo de exito correcto (201 Created). El read model
// que expone GET aun no existe (#625) -- este smoke solo verifica el efecto secundario real:
// mt_events en el schema programacion.
public class CrearPlantillaSemanalSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private const string SchemaProgramacion = "programacion";
    private const string TipoEventoPlantillaSemanalCreada = "plantilla_semanal_creada";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly HttpClient _client = api.Client;

    private static object PayloadValido(Guid? plantillaId = null, int semanas = 2)
    {
        var id = plantillaId ?? Guid.CreateVersion7();
        return new
        {
            plantillaId = id,
            nombre = $"[TEST] Plantilla {id}",
            semanas
        };
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearPlantillaSemanal_DebeRetornar201YPersistirPlantillaSemanalCreada_CuandoPayloadEsValido()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var plantillaId = Guid.CreateVersion7();
        var payload = PayloadValido(plantillaId);

        var response = await _client.PostAsJsonAsync(
            "/api/programacion/plantillas-semanales", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().EndWith(
            $"/programacion/plantillas-semanales/{plantillaId}");

        var existe = await postgres.ExisteEventoAsync(
            SchemaProgramacion, plantillaId.ToString(), TipoEventoPlantillaSemanalCreada, Timeout);
        existe.Should().BeTrue(
            $"el evento {TipoEventoPlantillaSemanalCreada} deberia existir en el stream {plantillaId}");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearPlantillaSemanal_DebeRetornar409_CuandoPlantillaYaExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var plantillaId = Guid.CreateVersion7();
        var payload = PayloadValido(plantillaId);

        await _client.PostAsJsonAsync("/api/programacion/plantillas-semanales", payload, ct);
        var response = await _client.PostAsJsonAsync("/api/programacion/plantillas-semanales", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearPlantillaSemanal_DebeRetornar400_CuandoSemanasEstaFueraDeRango()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadValido(semanas: 7);

        var response = await _client.PostAsJsonAsync("/api/programacion/plantillas-semanales", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
