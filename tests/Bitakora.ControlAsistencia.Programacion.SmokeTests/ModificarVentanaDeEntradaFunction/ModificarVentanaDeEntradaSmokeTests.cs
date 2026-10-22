using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.ModificarVentanaDeEntradaFunction;

public class ModificarVentanaDeEntradaSmokeTests(ApiFixture api, PostgresFixture postgres, ServiceBusFixture serviceBus)
{
    private const string RutaTurnos = "/api/programacion/turnos";
    private const string TipoEvento = "ventana_de_entrada_modificada";
    private const string TopicDisenoDeTurno = "diseno-de-turno-actualizado";
    private const string Suscripcion = "smoke-tests";

    private readonly HttpClient _client = api.Client;

    private static string Ruta(Guid turnoId) => $"{RutaTurnos}/{turnoId}/ventana-entrada";

    private static object Amplitud(int horas, int minutos = 0) => new { amplitud = new { horas, minutos } };

    private async Task<Guid> CrearTurnoConFranjaAsync(CancellationToken ct)
    {
        var turnoId = Guid.CreateVersion7();
        var payload = new
        {
            turnoId,
            nombre = $"[TEST] Turno Ventana {turnoId}",
            ordinarias = new[]
            {
                new
                {
                    inicio = "07:00:00",
                    fin = "16:00:00",
                    descansos = Array.Empty<object>(),
                    extras = Array.Empty<object>()
                }
            }
        };
        var response = await _client.PostAsJsonAsync(RutaTurnos, payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que CrearTurno funcione");
        return turnoId;
    }

    private async Task<Guid> CrearTurnoVacioAsync(CancellationToken ct)
    {
        var turnoId = Guid.CreateVersion7();
        var response = await _client.PostAsJsonAsync(
            RutaTurnos, new { turnoId, nombre = $"[TEST] Turno Ventana Vacio {turnoId}" }, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que CrearTurno funcione");
        return turnoId;
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task HealthCheck_DebeResponder200()
    {
        var response = await _client.GetAsync("/api/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ModificarVentanaDeEntrada_DebeRetornar204YPersistirElEvento_CuandoElTurnoTieneFranja()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");
        var ct = TestContext.Current.CancellationToken;
        var turnoId = await CrearTurnoConFranjaAsync(ct);

        var response = await _client.PutAsJsonAsync(Ruta(turnoId), Amplitud(3), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();
        (await postgres.ExisteEventoAsync(
            PostgresFixture.SchemaProgramacion, turnoId.ToString(), TipoEvento, TimeSpan.FromSeconds(30)))
            .Should().BeTrue("la ventana de entrada modificada debe quedar persistida en el stream del turno");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ModificarVentanaDeEntrada_DebeRetornar204SinEventoAdicional_CuandoLaAmplitudYaEstaAlcanzada()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");
        var ct = TestContext.Current.CancellationToken;
        var turnoId = await CrearTurnoConFranjaAsync(ct);
        (await _client.PutAsJsonAsync(Ruta(turnoId), Amplitud(3), ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var lineaBase = await postgres.ContarEventosAsync(
            PostgresFixture.SchemaProgramacion, turnoId.ToString(), TipoEvento);
        lineaBase.Should().Be(1);

        var repetida = await _client.PutAsJsonAsync(Ruta(turnoId), Amplitud(3), ct);

        repetida.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await postgres.ContarEventosAsync(PostgresFixture.SchemaProgramacion, turnoId.ToString(), TipoEvento))
            .Should().Be(lineaBase);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ModificarVentanaDeEntrada_DebeRetornar204SinEvento_CuandoPideAmplitudCeroSobreTurnoFijo()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");
        var ct = TestContext.Current.CancellationToken;
        var turnoId = await CrearTurnoConFranjaAsync(ct);

        var response = await _client.PutAsJsonAsync(Ruta(turnoId), Amplitud(0), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await postgres.ContarEventosAsync(PostgresFixture.SchemaProgramacion, turnoId.ToString(), TipoEvento))
            .Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ModificarVentanaDeEntrada_DebeRetornar409_CuandoElTurnoNoTieneFranjas()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoId = await CrearTurnoVacioAsync(ct);

        var response = await _client.PutAsJsonAsync(Ruta(turnoId), Amplitud(3), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ModificarVentanaDeEntrada_DebeRetornar404_CuandoElTurnoNoExiste()
    {
        var response = await _client.PutAsJsonAsync(
            Ruta(Guid.CreateVersion7()), Amplitud(3), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ModificarVentanaDeEntrada_DebeRetornar400_CuandoElIdNoEsGuid()
    {
        var response = await _client.PutAsJsonAsync(
            $"{RutaTurnos}/no-es-guid/ventana-entrada", Amplitud(3), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ModificarVentanaDeEntrada_DebeRetornar400_CuandoLosMinutosEstanFueraDeRango()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoId = await CrearTurnoConFranjaAsync(ct);

        var response = await _client.PutAsJsonAsync(Ruta(turnoId), Amplitud(1, 60), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ModificarVentanaDeEntrada_PublicaDisenoDeTurnoActualizadoConLaAmplitud_CuandoElTurnoTieneFranja()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        var ct = TestContext.Current.CancellationToken;
        var turnoId = await CrearTurnoConFranjaAsync(ct);
        await serviceBus.PurgeAsync(TopicDisenoDeTurno, Suscripcion);

        var response = await _client.PutAsJsonAsync(Ruta(turnoId), Amplitud(3), ct);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var evento = await serviceBus.WaitForMessageAsync<DisenoDeTurnoActualizado>(
            TopicDisenoDeTurno, Suscripcion, e => e.TurnoId == turnoId, TimeSpan.FromSeconds(30));

        evento.AmplitudVentanaEntradaEnMinutos.Should().Be(180);
        evento.Version.Should().Be(2);
        evento.Retirado.Should().BeFalse();
        var franja = evento.Franjas.Should().ContainSingle().Which;
        franja.HoraInicio.Should().Be(new TimeOnly(7, 0));
        franja.HoraFin.Should().Be(new TimeOnly(16, 0));
    }
}
