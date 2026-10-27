using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.AsignarTurnoADiaDePlantillaTurnosFunction;

public class AsignarTurnoADiaDePlantillaTurnosSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private const string SchemaProgramacion = "programacion";
    private const string TipoEventoDiaAsignado = "dia_de_plantilla_de_turnos_asignado";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly HttpClient _client = api.Client;

    private async Task<Guid> CrearPlantillaAsync(CancellationToken ct, int diasDelCiclo = 8)
    {
        var plantillaId = Guid.CreateVersion7();
        var response = await _client.PostAsJsonAsync(
            "/api/programacion/plantillas-turnos",
            new { plantillaId, nombre = $"[TEST] Plantilla {plantillaId}", diasDelCiclo }, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que CrearPlantillaTurnos funcione");
        return plantillaId;
    }

    private async Task<Guid> CrearTurnoAsync(CancellationToken ct)
    {
        var turnoId = Guid.CreateVersion7();
        var response = await _client.PostAsJsonAsync("/api/programacion/turnos", new
        {
            turnoId,
            nombre = $"[TEST] Turno {turnoId}",
            ordinarias = new[]
            {
                new
                {
                    inicio = "06:00:00",
                    fin = "14:00:00",
                    descansos = Array.Empty<object>(),
                    extras = Array.Empty<object>()
                }
            }
        }, ct);
        response.IsSuccessStatusCode.Should().BeTrue(
            "el arrange de este smoke test depende de que CrearTurno acepte el comando");
        return turnoId;
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
    public async Task AsignarTurnoADia_DebeRetornar204YPersistirElDia_CuandoLaPlantillaYElTurnoExisten()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var plantillaId = await CrearPlantillaAsync(ct);
        var turnoId = await CrearTurnoAsync(ct);

        var response = await _client.PutAsJsonAsync(
            $"/api/programacion/plantillas-turnos/{plantillaId}/dias/3", new { turnoId }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaProgramacion, plantillaId.ToString(), TipoEventoDiaAsignado,
            campoJson: "TurnoId", valorJson: turnoId.ToString(), Timeout);

        eventoPersistido.GetProperty("Dia").GetInt32().Should().Be(3);
        eventoPersistido.TryGetProperty("Turno", out var copia).Should().BeTrue();
        copia.ValueKind.Should().Be(JsonValueKind.Object);
        eventoPersistido.GetProperty("VersionTurno").GetInt64().Should().BeGreaterThan(0);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarTurnoADia_DebeRetornar204SinNuevosEventos_CuandoElDiaYaTieneElTurnoConCopiaVigente()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var plantillaId = await CrearPlantillaAsync(ct);
        var turnoId = await CrearTurnoAsync(ct);
        var ruta = $"/api/programacion/plantillas-turnos/{plantillaId}/dias/2";

        var primero = await _client.PutAsJsonAsync(ruta, new { turnoId }, ct);
        primero.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var streamId = plantillaId.ToString();
        await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaProgramacion, streamId, TipoEventoDiaAsignado,
            campoJson: "TurnoId", valorJson: turnoId.ToString(), Timeout);
        var eventosAntes = await postgres.ContarEventosDeStreamAsync(
            SchemaProgramacion, postgres.TenantId, streamId);

        var segundo = await _client.PutAsJsonAsync(ruta, new { turnoId }, ct);
        segundo.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var eventosDespues = await postgres.ContarEventosDeStreamAsync(
            SchemaProgramacion, postgres.TenantId, streamId);
        eventosDespues.Should().Be(eventosAntes);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarTurnoADia_DebeRetornar409_CuandoElDiaSuperaLosDiasDelCiclo()
    {
        var ct = TestContext.Current.CancellationToken;
        var plantillaId = await CrearPlantillaAsync(ct, diasDelCiclo: 8);
        var turnoId = await CrearTurnoAsync(ct);

        var response = await _client.PutAsJsonAsync(
            $"/api/programacion/plantillas-turnos/{plantillaId}/dias/9", new { turnoId }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarTurnoADia_DebeRetornar400_CuandoElDiaEsCero()
    {
        var ct = TestContext.Current.CancellationToken;
        var plantillaId = await CrearPlantillaAsync(ct);
        var turnoId = await CrearTurnoAsync(ct);

        var response = await _client.PutAsJsonAsync(
            $"/api/programacion/plantillas-turnos/{plantillaId}/dias/0", new { turnoId }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarTurnoADia_DebeRetornar404_CuandoElTurnoNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var plantillaId = await CrearPlantillaAsync(ct);

        var response = await _client.PutAsJsonAsync(
            $"/api/programacion/plantillas-turnos/{plantillaId}/dias/1",
            new { turnoId = Guid.CreateVersion7() }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarTurnoADia_DebeRetornar204_CuandoElDiaEsElUltimoDelCiclo()
    {
        var ct = TestContext.Current.CancellationToken;
        var plantillaId = await CrearPlantillaAsync(ct, diasDelCiclo: 8);
        var turnoId = await CrearTurnoAsync(ct);

        var response = await _client.PutAsJsonAsync(
            $"/api/programacion/plantillas-turnos/{plantillaId}/dias/8", new { turnoId }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarTurnoADia_DebeRetornar404_CuandoLaPlantillaNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoId = await CrearTurnoAsync(ct);

        var response = await _client.PutAsJsonAsync(
            $"/api/programacion/plantillas-turnos/{Guid.CreateVersion7()}/dias/1", new { turnoId }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
