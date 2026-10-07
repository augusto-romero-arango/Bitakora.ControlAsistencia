using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.AsignarJornadaPredeterminadaFunction;

public class AsignarJornadaPredeterminadaSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private const string RutaJornadas = "/api/programacion/jornadas";
    private const string RutaPredeterminada = "/api/programacion/preferencias/jornada-predeterminada";
    private const string TipoEvento = "jornada_predeterminada_asignada";
    private readonly HttpClient _client = api.Client;

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task HealthCheck_DebeResponder200_CuandoElEntornoEstaDisponible()
    {
        var response = await _client.GetAsync("/api/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarJornadaPredeterminada_DebeRetornar204YPersistirLaPredeterminada_CuandoLaJornadaExiste()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");
        var ct = TestContext.Current.CancellationToken;
        var jornadaId = Guid.CreateVersion7();
        var creada = await _client.PostAsJsonAsync(RutaJornadas, new
        {
            jornadaId,
            horasSemanales = new { horas = 30, minutos = Random.Shared.Next(1, 60) },
            topeDiario = new { horas = 8, minutos = 0 },
            minimoDiario = new { horas = 0, minutos = 0 },
            diasDescansoPorSemana = 1
        }, ct);
        creada.StatusCode.Should().Be(HttpStatusCode.Created);
        var streamPreferencias = PostgresFixture.StreamIdPreferencias(postgres.TenantId);
        var asignacionesAntes = await postgres.ContarEventosAsync(
            PostgresFixture.SchemaProgramacion, streamPreferencias, TipoEvento);

        var asignada = await _client.PutAsJsonAsync(RutaPredeterminada, new { jornadaId }, ct);

        asignada.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await asignada.Content.ReadAsStringAsync(ct)).Should().BeEmpty();
        (await postgres.ContarEventosAsync(PostgresFixture.SchemaProgramacion, streamPreferencias, TipoEvento))
            .Should().Be(asignacionesAntes + 1);
        var eventos = await postgres.LeerEventosDeStreamAsync(
            PostgresFixture.SchemaProgramacion, postgres.TenantId, streamPreferencias);
        eventos[^1].GetProperty("jornadaId").GetGuid().Should().Be(jornadaId);

        var eventosTrasPrimera = await postgres.ContarEventosDeStreamAsync(
            PostgresFixture.SchemaProgramacion, postgres.TenantId, streamPreferencias);
        var repetida = await _client.PutAsJsonAsync(RutaPredeterminada, new { jornadaId }, ct);

        repetida.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await postgres.ContarEventosDeStreamAsync(
            PostgresFixture.SchemaProgramacion, postgres.TenantId, streamPreferencias))
            .Should().Be(eventosTrasPrimera);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarJornadaPredeterminada_DebeRetornar404_CuandoLaJornadaNoExiste()
    {
        var response = await _client.PutAsJsonAsync(RutaPredeterminada,
            new { jornadaId = Guid.CreateVersion7() }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarJornadaPredeterminada_DebeRetornar400_CuandoFaltaElJornadaId()
    {
        var response = await _client.PutAsJsonAsync(RutaPredeterminada,
            new { }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
