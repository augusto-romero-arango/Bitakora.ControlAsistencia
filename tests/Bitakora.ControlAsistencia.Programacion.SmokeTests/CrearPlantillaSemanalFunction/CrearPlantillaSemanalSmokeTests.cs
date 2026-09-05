using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.CrearPlantillaSemanalFunction;

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
    public async Task HealthCheck_DebeResponder200()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
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

    private async Task<bool> PlantillaEstaMaterializadaAsync(Guid plantillaId, CancellationToken ct)
    {
        var response = await _client.GetAsync($"/api/programacion/plantillas-semanales/{plantillaId}", ct);
        return response.StatusCode == HttpStatusCode.OK;
    }

    // Espera a que CuadroSemanalTurnos materialice la plantilla antes del POST duplicado: sin esto,
    // un 201/409 inmediato no distingue "la comparacion contra el catalogo funciono" de "la
    // proyeccion Async (MEF-ADR-0034 seccion 3) aun no vio nada" (best-effort, CA-ADR-0034
    // decision 4).
    private async Task EsperarPlantillaMaterializadaAsync(Guid plantillaId, CancellationToken ct) =>
        await Polling.WaitUntilTrueAsync(
            async () => await PlantillaEstaMaterializadaAsync(plantillaId, ct), Timeout);

    // CA-1/CA-2: nombre coincide exactamente, o solo difiere en mayusculas/espacios de sobra, con
    // el de un cuadro vigente -> 409 NombreDuplicado. Espejo de CrearTurno (#497).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearPlantillaSemanal_DebeRetornar409_CuandoElNombreYaExisteEnElCatalogo()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = Guid.CreateVersion7();
        var nombreExistente = $"[TEST] Cocina {sufijo}";
        var plantillaExistenteId = Guid.CreateVersion7();

        var arrange = await _client.PostAsJsonAsync(
            "/api/programacion/plantillas-semanales",
            new { plantillaId = plantillaExistenteId, nombre = nombreExistente, semanas = 1 }, ct);
        arrange.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que CrearPlantillaSemanal funcione");
        await EsperarPlantillaMaterializadaAsync(plantillaExistenteId, ct);

        var duplicado = await _client.PostAsJsonAsync(
            "/api/programacion/plantillas-semanales",
            new
            {
                plantillaId = Guid.CreateVersion7(),
                nombre = $"  [test]  cocina {sufijo} ",
                semanas = 1
            }, ct);

        duplicado.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Los acentos son significativos (decision del experto en #497): un nombre que difiere
        // solo en acentos SI se crea.
        var conAcento = await _client.PostAsJsonAsync(
            "/api/programacion/plantillas-semanales",
            new
            {
                plantillaId = Guid.CreateVersion7(),
                nombre = $"[TEST] Cociña {sufijo}",
                semanas = 1
            }, ct);

        conAcento.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // El retiro (#623/#624) libera el nombre: la vista solo ve plantillas vigentes.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearPlantillaSemanal_LiberaElNombre_CuandoLaPlantillaSeRetira()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = Guid.CreateVersion7();
        var nombreOriginal = $"[TEST] Bodega {sufijo}";
        var plantillaOriginalId = Guid.CreateVersion7();

        var arrange = await _client.PostAsJsonAsync(
            "/api/programacion/plantillas-semanales",
            new { plantillaId = plantillaOriginalId, nombre = nombreOriginal, semanas = 1 }, ct);
        arrange.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que CrearPlantillaSemanal funcione");
        await EsperarPlantillaMaterializadaAsync(plantillaOriginalId, ct);

        var retiro = await _client.DeleteAsync(
            $"/api/programacion/plantillas-semanales/{plantillaOriginalId}", ct);
        retiro.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que RetirarPlantillaSemanal funcione");

        await Polling.WaitUntilTrueAsync(
            async () => !await PlantillaEstaMaterializadaAsync(plantillaOriginalId, ct), Timeout);

        var reutilizado = await _client.PostAsJsonAsync(
            "/api/programacion/plantillas-semanales",
            new { plantillaId = Guid.CreateVersion7(), nombre = nombreOriginal, semanas = 1 }, ct);

        reutilizado.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
