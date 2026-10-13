using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.ModificarLimitesJornadaFunction;

public class ModificarLimitesJornadaSmokeTests(ApiFixture api, PostgresFixture postgres, ServiceBusFixture serviceBus)
    : IDisposable
{
    private readonly TenantDeTest _tenant = new(api);

    public void Dispose() => _tenant.Dispose();

    private const string Ruta = "/api/programacion/jornadas";
    private const string TopicSalida = "limites-de-jornada-actualizados";
    private const string Suscripcion = "smoke-tests";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private HttpClient _client => _tenant.Client;

    private static object Limites(int horas, int minutos, int topeHoras = 8, int topeMinutos = 0) => new
    {
        horasSemanales = new { horas, minutos },
        topeDiario = new { horas = topeHoras, minutos = topeMinutos },
        minimoDiario = new { horas = 0, minutos = 0 },
        diasDescansoPorSemana = 1
    };

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task HealthCheck_DebeResponder200_CuandoElEntornoEstaDisponible()
    {
        var response = await _client.GetAsync("/api/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ModificarLimitesJornada_DebeRetornar204YPersistirLosLimites_CuandoPayloadEsValido()
    {
        var origen = LimitesDePrueba.ModificarOrigen;
        var firmaDestino = LimitesDePrueba.ModificarDestino;
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.CreateVersion7();
        var creada = await _client.PostAsJsonAsync(Ruta, origen.PayloadCrear(id), ct);
        creada.StatusCode.Should().Be(HttpStatusCode.Created);
        var destino = firmaDestino.Limites();

        var modificada = await _client.PutAsJsonAsync($"{Ruta}/{id}/limites", destino, ct);

        modificada.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var lectura = await _client.GetAsync($"{Ruta}/{id}", ct);
        lectura.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await lectura.Content.ReadAsStringAsync(ct));
        var semanales = json.RootElement.GetProperty("horasSemanales");
        semanales.GetProperty("horas").GetInt32().Should().Be(firmaDestino.SemanalesHoras);
        semanales.GetProperty("minutos").GetInt32().Should().Be(firmaDestino.SemanalesMinutos);

        (await _client.PutAsJsonAsync($"{Ruta}/{id}/limites", destino, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await postgres.ContarEventosAsync("programacion", id.ToString(), "limites_jornada_modificados"))
            .Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ModificarLimitesJornada_DebeRetornar409_CuandoLosLimitesIgualanAOtraJornada()
    {
        var firmaA = LimitesDePrueba.ModificarConflictoA;
        var firmaB = LimitesDePrueba.ModificarConflictoB;
        var ct = TestContext.Current.CancellationToken;
        var idA = Guid.CreateVersion7();
        var idB = Guid.CreateVersion7();
        (await _client.PostAsJsonAsync(Ruta, firmaA.PayloadCrear(idA), ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await _client.PostAsJsonAsync(Ruta, firmaB.PayloadCrear(idB), ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        await Polling.WaitUntilTrueAsync(async () =>
        {
            var response = await _client.GetAsync(Ruta, ct);
            return (await response.Content.ReadAsStringAsync(ct)).Contains(idA.ToString());
        }, Timeout);

        var conflicto = await _client.PutAsJsonAsync($"{Ruta}/{idB}/limites", firmaA.Limites(), ct);

        conflicto.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await conflicto.Content.ReadAsStringAsync(ct)).Should().Contain(idA.ToString());
        (await _client.PutAsJsonAsync($"{Ruta}/{idA}/limites", firmaA.Limites(), ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ModificarLimitesJornada_PublicaLimitesDeJornadaActualizadosConVersion2_CuandoLosLimitesCambian()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        var ct = TestContext.Current.CancellationToken;
        var origen = LimitesDePrueba.ModificarPublicaOrigen;
        var destino = LimitesDePrueba.ModificarPublicaDestino;
        await serviceBus.PurgeAsync(TopicSalida, Suscripcion);
        var id = Guid.CreateVersion7();
        (await _client.PostAsJsonAsync(Ruta, origen.PayloadCrear(id), ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var modificada = await _client.PutAsJsonAsync($"{Ruta}/{id}/limites", destino.Limites(), ct);

        modificada.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var evento = await serviceBus.WaitForMessageAsync<LimitesDeJornadaActualizados>(
            TopicSalida, Suscripcion, e => e.JornadaId == id, Timeout);
        evento.HorasSemanalesEnMinutos.Should().Be(destino.HorasSemanalesEnMinutos);
        evento.TopeDiarioEnMinutos.Should().Be(destino.TopeDiarioEnMinutos);
        evento.MinimoDiarioEnMinutos.Should().Be(destino.MinimoDiarioEnMinutos);
        evento.DiasDescansoPorSemana.Should().Be(destino.DiasDescansoPorSemana);
        evento.Version.Should().Be(2);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ModificarLimitesJornada_NoPublica_CuandoLosLimitesYaEstanAlcanzados()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        var ct = TestContext.Current.CancellationToken;
        var origen = LimitesDePrueba.ModificarNoPublicaOrigen;
        var firmaDestino = LimitesDePrueba.ModificarNoPublicaDestino;
        await serviceBus.PurgeAsync(TopicSalida, Suscripcion);
        var id = Guid.CreateVersion7();
        (await _client.PostAsJsonAsync(Ruta, origen.PayloadCrear(id), ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        var destino = firmaDestino.Limites();
        (await _client.PutAsJsonAsync($"{Ruta}/{id}/limites", destino, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        await serviceBus.WaitForMessageAsync<LimitesDeJornadaActualizados>(
            TopicSalida, Suscripcion, e => e.JornadaId == id, Timeout);

        var repetida = await _client.PutAsJsonAsync($"{Ruta}/{id}/limites", destino, ct);

        repetida.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await Assert.ThrowsAsync<TimeoutException>(() =>
            serviceBus.WaitForMessageAsync<LimitesDeJornadaActualizados>(
                TopicSalida, Suscripcion, e => e.JornadaId == id, TimeSpan.FromSeconds(3)));
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ModificarLimitesJornada_DebeRetornar404_CuandoLaJornadaNoExiste()
    {
        var response = await _client.PutAsJsonAsync($"{Ruta}/{Guid.CreateVersion7()}/limites",
            Limites(31, 17), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ModificarLimitesJornada_DebeRetornar400_CuandoElIdNoEsGuid()
    {
        var response = await _client.PutAsJsonAsync($"{Ruta}/no-es-guid/limites",
            Limites(31, 17), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ModificarLimitesJornada_DebeRetornar400_CuandoElTopeDiarioEsInvalido()
    {
        var origen = LimitesDePrueba.ModificarTopeInvalido;
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.CreateVersion7();
        (await _client.PostAsJsonAsync(Ruta, origen.PayloadCrear(id), ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await _client.PutAsJsonAsync($"{Ruta}/{id}/limites",
            Limites(origen.SemanalesHoras + 1, origen.SemanalesMinutos, topeHoras: 0, topeMinutos: 60), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
