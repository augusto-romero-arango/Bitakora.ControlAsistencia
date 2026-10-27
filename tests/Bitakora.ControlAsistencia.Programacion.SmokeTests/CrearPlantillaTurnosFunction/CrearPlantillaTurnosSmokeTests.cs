using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.CrearPlantillaTurnosFunction;

public class CrearPlantillaTurnosSmokeTests(ApiFixture api, PostgresFixture postgres)
    : IDisposable
{
    private readonly TenantDeTest _tenant = new(api);

    public void Dispose() => _tenant.Dispose();

    private const string Ruta = "/api/programacion/plantillas-turnos";
    private const string SchemaProgramacion = "programacion";
    private const string TipoEventoCreada = "plantilla_de_turnos_creada";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private HttpClient _client => _tenant.Client;

    private static object Payload(Guid? plantillaId = null, int diasDelCiclo = 8)
    {
        var id = plantillaId ?? Guid.CreateVersion7();
        return new { plantillaId = id, nombre = $"[TEST] Plantilla {id}", diasDelCiclo };
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task HealthCheck_DebeResponder200()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearPlantillaTurnos_DebeRetornar201YPersistirPlantillaDeTurnosCreada_CuandoPayloadEsValido()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var plantillaId = Guid.CreateVersion7();

        var response = await _client.PostAsJsonAsync(Ruta, Payload(plantillaId), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().EndWith($"/programacion/plantillas-turnos/{plantillaId}");
        (await postgres.ExisteEventoAsync(
            SchemaProgramacion, plantillaId.ToString(), TipoEventoCreada, Timeout))
            .Should().BeTrue($"el evento {TipoEventoCreada} deberia existir en el stream {plantillaId}");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearPlantillaTurnos_DebeRetornar409_CuandoPlantillaYaExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = Payload();

        await _client.PostAsJsonAsync(Ruta, payload, ct);
        var response = await _client.PostAsJsonAsync(Ruta, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearPlantillaTurnos_DebeRetornar400_CuandoDiasDelCicloEstaFueraDeRango()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync(Ruta, Payload(diasDelCiclo: 43), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
