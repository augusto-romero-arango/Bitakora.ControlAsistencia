using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.CrearTurnoDeHorarioAbiertoFunction;

public class CrearTurnoDeHorarioAbiertoSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaTurnos = "/api/programacion/turnos";
    private const string RutaHorarioAbierto = "/api/programacion/turnos/horario-abierto";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record FranjaRespuestaSmoke(TimeOnly HoraInicio, TimeOnly HoraFin, int DiaOffsetFin);

    private sealed record VentanaRespuestaSmoke(TimeOnly Apertura, TimeOnly Cierre, string Amplitud);

    private sealed record FichaRespuestaSmoke(
        string Id,
        IReadOnlyList<FranjaRespuestaSmoke> Franjas,
        VentanaRespuestaSmoke? VentanaDeEntrada);

    private static object Payload(Guid turnoId, int horas, int minutos = 0) => new
    {
        turnoId,
        nombre = $"[TEST] Horario Abierto {turnoId}",
        duracion = new { horas, minutos }
    };

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task HealthCheck_DebeResponder200()
    {
        var response = await _client.GetAsync("/api/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearTurnoDeHorarioAbierto_DebeRetornar201ConLocationYMostrarFranjaYVentanaEnLaFicha_CuandoDuracionEsValida()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();

        var response = await _client.PostAsJsonAsync(RutaHorarioAbierto, Payload(turnoId, 6), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().Be(new Uri($"/api/programacion/turnos/{turnoId}", UriKind.Relative));

        FichaRespuestaSmoke? ficha = null;
        var completa = await Polling.WaitUntilTrueAsync(async () =>
        {
            var get = await _client.GetAsync($"{RutaTurnos}/{turnoId}", ct);
            if (get.StatusCode != HttpStatusCode.OK)
                return false;

            ficha = await get.Content.ReadFromJsonAsync<FichaRespuestaSmoke>(JsonOptions, ct);
            return ficha?.VentanaDeEntrada is not null && ficha.Franjas.Count == 1;
        }, Timeout);

        completa.Should().BeTrue("la ficha debe reflejar la franja y la ventana que deja el arrange");
        ficha!.Franjas.Should().ContainSingle().Which.Should().Be(
            new FranjaRespuestaSmoke(new TimeOnly(4, 0), new TimeOnly(10, 0), 0));
        ficha.VentanaDeEntrada.Should().Be(
            new VentanaRespuestaSmoke(new TimeOnly(4, 0), new TimeOnly(18, 0), "14 h"));
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearTurnoDeHorarioAbierto_DebeRetornar201SinVentana_CuandoDuracionEs20Horas()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");
        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();

        var response = await _client.PostAsJsonAsync(RutaHorarioAbierto, Payload(turnoId, 20), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var streamId = turnoId.ToString();
        var creado = await postgres.ObtenerEventoAsync<JsonElement>(
            PostgresFixture.SchemaProgramacion, streamId, "turno_creado",
            campoJson: "Nombre", valorJson: $"[TEST] Horario Abierto {turnoId}", Timeout);

        creado.GetProperty("FranjasOrdinarias")[0]
            .GetProperty("diaOffsetFin").GetInt32().Should().Be(1);

        (await postgres.ExisteEventoAsync(
            PostgresFixture.SchemaProgramacion, streamId, "ventana_de_entrada_modificada",
            TimeSpan.FromSeconds(3)))
            .Should().BeFalse("con duracion 20 h la amplitud es 0 y no se emite la ventana");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearTurnoDeHorarioAbierto_DebeRetornar400_CuandoDuracionEsCero()
    {
        var response = await _client.PostAsJsonAsync(
            RutaHorarioAbierto, Payload(Guid.CreateVersion7(), 0), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CrearTurnoDeHorarioAbierto_DebeRetornar409_CuandoTurnoYaExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = Payload(Guid.CreateVersion7(), 8);

        var primero = await _client.PostAsJsonAsync(RutaHorarioAbierto, payload, ct);
        primero.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange depende de que el primer POST funcione");

        var response = await _client.PostAsJsonAsync(RutaHorarioAbierto, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
