using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;
using static Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures.DatosDePrueba;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.AsignarJornadaFunction;

public class AsignarJornadaSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private const string Schema = "colaboradores";
    private const string Alias = "jornada_asignada";

    private static string NuevoNumero() => Guid.CreateVersion7().ToString("N").ToUpperInvariant();
    private static string StreamId(string numero) => $"CC-{numero}";
    private static string Ruta(string id) => $"/api/colaboradores/{id}/jornada";

    private async Task<string> RegistrarAsync(string numero, CancellationToken ct)
    {
        var codigo = NuevoCodigoColaborador();
        var response = await _client.PostAsJsonAsync("/api/colaboradores", new
        {
            tipoIdentificacion = "CC",
            numeroIdentificacion = numero,
            primerNombre = "[TEST]",
            segundoNombre = (string?)null,
            primerApellido = "Smoke",
            segundoApellido = (string?)null,
            codigoColaborador = codigo,
            fechaInicio = new DateOnly(2026, 1, 15)
        }, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return codigo;
    }

    private Task<HttpResponseMessage> AsignarAsync(string id, Guid jornadaId, CancellationToken ct) =>
        _client.PutAsJsonAsync(Ruta(id), new { jornadaId }, ct);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task Health_Retorna200_CuandoSeConsulta()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarJornada_Retorna204YPersisteJornadaAsignada_CuandoColaboradorExiste()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");
        var ct = TestContext.Current.CancellationToken;
        var numero = NuevoNumero();
        var id = StreamId(numero);
        var jornadaId = Guid.CreateVersion7();
        await RegistrarAsync(numero, ct);

        var response = await AsignarAsync(id, jornadaId, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();
        var evento = await postgres.ObtenerEventoAsync<JsonElement>(Schema, id, Alias, Timeout);
        evento.GetProperty("JornadaId").GetGuid().Should().Be(jornadaId);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarJornada_Retorna204YPersisteOtraJornada_CuandoReasigna()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");
        var ct = TestContext.Current.CancellationToken;
        var numero = NuevoNumero();
        var id = StreamId(numero);
        var primeraJornadaId = Guid.CreateVersion7();
        var nuevaJornadaId = Guid.CreateVersion7();
        await RegistrarAsync(numero, ct);

        var primeraRespuesta = await AsignarAsync(id, primeraJornadaId, ct);
        primeraRespuesta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await postgres.ExisteEventoAsync(Schema, id, Alias, Timeout, "JornadaId", primeraJornadaId.ToString())).Should().BeTrue();

        var response = await AsignarAsync(id, nuevaJornadaId, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();
        (await postgres.ContarEventosAsync(Schema, id, Alias)).Should().Be(2);
        var evento = await postgres.ObtenerEventoAsync<JsonElement>(
            Schema, id, Alias, "JornadaId", nuevaJornadaId.ToString(), Timeout);
        evento.GetProperty("JornadaId").GetGuid().Should().Be(nuevaJornadaId);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarJornada_Retorna204SinNuevoEvento_CuandoRepiteElMismoPut()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");
        var ct = TestContext.Current.CancellationToken;
        var numero = NuevoNumero();
        var id = StreamId(numero);
        var jornadaId = Guid.CreateVersion7();
        await RegistrarAsync(numero, ct);
        var primeraRespuesta = await AsignarAsync(id, jornadaId, ct);
        primeraRespuesta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await primeraRespuesta.Content.ReadAsStringAsync(ct)).Should().BeEmpty();
        (await postgres.ExisteEventoAsync(Schema, id, Alias, Timeout, "JornadaId", jornadaId.ToString())).Should().BeTrue();
        var eventosAntes = await postgres.ContarEventosDelStreamAsync(Schema, id);

        var response = await AsignarAsync(id, jornadaId, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();
        (await postgres.ContarEventosDelStreamAsync(Schema, id)).Should().Be(eventosAntes);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarJornada_Retorna404_CuandoColaboradorNoExiste()
    {
        var response = await AsignarAsync(StreamId(NuevoNumero()), Guid.CreateVersion7(),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarJornada_Retorna409_CuandoVinculacionTermino()
    {
        var ct = TestContext.Current.CancellationToken;
        var numero = NuevoNumero();
        var id = StreamId(numero);
        var codigo = await RegistrarAsync(numero, ct);
        var terminacion = await _client.PostAsJsonAsync(
            $"/api/colaboradores/{id}/vinculaciones/{codigo}:terminar",
            new { fechaEfectiva = new DateOnly(2030, 12, 31) }, ct);
        terminacion.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await AsignarAsync(id, Guid.CreateVersion7(), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarJornada_Retorna400_CuandoJornadaIdEsVacio()
    {
        var response = await AsignarAsync(StreamId(NuevoNumero()), Guid.Empty,
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarJornada_Retorna400_CuandoIdDeRutaNoTieneTipo()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await AsignarAsync(NuevoNumero(), Guid.CreateVersion7(), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
