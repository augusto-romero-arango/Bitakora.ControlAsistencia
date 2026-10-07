using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.AsignarJornadaAPlantillaSemanalFunction;

public class AsignarJornadaAPlantillaSemanalSmokeTests(ApiFixture api)
{
    private readonly HttpClient _client = api.Client;

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task HealthCheck_DebeResponder200()
    {
        var response = await _client.GetAsync("/api/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarYQuitarJornada_DebeRetornar204EnCadaPasoY404_CuandoLaJornadaNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var plantillaId = Guid.CreateVersion7();
        var jornadaId = Guid.CreateVersion7();
        var minutos = Random.Shared.Next(1, 60);

        (await _client.PostAsJsonAsync("/api/programacion/plantillas-semanales", new
        {
            plantillaId,
            nombre = $"[TEST] Plantilla {plantillaId}",
            semanas = 1
        }, ct)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await _client.PostAsJsonAsync("/api/programacion/jornadas", new
        {
            jornadaId,
            horasSemanales = new { horas = 30, minutos },
            topeDiario = new { horas = 8, minutos = 0 },
            minimoDiario = new { horas = 0, minutos = 0 },
            diasDescansoPorSemana = 1
        }, ct)).StatusCode.Should().Be(HttpStatusCode.Created);
        var ruta = $"/api/programacion/plantillas-semanales/{plantillaId}/jornada";

        (await _client.PutAsJsonAsync(ruta, new { jornadaId }, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.PutAsJsonAsync(ruta, new { jornadaId }, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.DeleteAsync(ruta, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.PutAsJsonAsync(ruta, new { jornadaId = Guid.CreateVersion7() }, ct))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
