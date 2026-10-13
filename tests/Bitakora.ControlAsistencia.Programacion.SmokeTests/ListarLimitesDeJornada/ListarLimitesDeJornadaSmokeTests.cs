using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.ListarLimitesDeJornada;

public class ListarLimitesDeJornadaSmokeTests(ApiFixture api, PostgresFixture postgres) : IDisposable
{
    private readonly TenantDeTest _tenant = new(api);

    public void Dispose() => _tenant.Dispose();

    private const string Ruta = "/api/programacion/jornadas";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private HttpClient _client => _tenant.Client;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record HorasYMinutosSmoke(int Horas, int Minutos);

    private sealed record ElementoSmoke(
        Guid JornadaId,
        HorasYMinutosSmoke HorasSemanales,
        HorasYMinutosSmoke TopeDiario,
        HorasYMinutosSmoke MinimoDiario,
        int DiasDescansoPorSemana,
        string Descripcion,
        bool EsPredeterminada);

    private sealed record ListaSmoke(IReadOnlyList<ElementoSmoke> Elementos, string? SiguienteCursor);

    private async Task<Guid> CrearJornadaAsync(FirmaDeLimites firma, CancellationToken ct)
    {
        var id = Guid.CreateVersion7();
        var response = await _client.PostAsJsonAsync(Ruta, firma.PayloadCrear(id), ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que CrearJornada funcione");
        return id;
    }

    private Task<ListaSmoke> ListarAsync(string query, CancellationToken ct) =>
        ListarAsync(_client, query, ct);

    private static async Task<ListaSmoke> ListarAsync(HttpClient client, string query, CancellationToken ct)
    {
        var response = await client.GetAsync($"{Ruta}{query}", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var lista = await response.Content.ReadFromJsonAsync<ListaSmoke>(JsonOptions, cancellationToken: ct);
        return lista!;
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarLimitesDeJornada_IncluyeLaJornadaCreadaUnaVez_CuandoLaProyeccionMaterializa()
    {
        var ct = TestContext.Current.CancellationToken;
        var firma = LimitesDePrueba.ListarIncluyeLaCreada;
        var id = await CrearJornadaAsync(firma, ct);

        var lista = await Polling.WaitUntilAsync(async () =>
        {
            var l = await ListarAsync("", ct);
            return l.Elementos.Any(e => e.JornadaId == id) ? l : null;
        }, Timeout);

        var elemento = lista.Elementos.Should().ContainSingle(e => e.JornadaId == id).Subject;
        elemento.HorasSemanales.Should().Be(new HorasYMinutosSmoke(firma.SemanalesHoras, firma.SemanalesMinutos));
        elemento.TopeDiario.Should().Be(new HorasYMinutosSmoke(firma.TopeHoras, firma.TopeMinutos));
        elemento.DiasDescansoPorSemana.Should().Be(firma.DiasDescansoPorSemana);
        elemento.Descripcion.Should().NotBeNullOrWhiteSpace();
        lista.SiguienteCursor.Should().BeNull("sin take se devuelve todo");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarLimitesDeJornada_PaginaConCursorSinRepetir_CuandoSeUsaTake()
    {
        var ct = TestContext.Current.CancellationToken;
        var id1 = await CrearJornadaAsync(LimitesDePrueba.ListarPaginaPrimera, ct);
        var id2 = await CrearJornadaAsync(LimitesDePrueba.ListarPaginaSegunda, ct);

        await Polling.WaitUntilAsync(async () =>
        {
            var l = await ListarAsync("", ct);
            var ids = l.Elementos.Select(e => e.JornadaId).ToList();
            return ids.Contains(id1) && ids.Contains(id2) ? l : null;
        }, Timeout);

        var vistos = new List<Guid>();
        var query = "?take=1";
        for (var i = 0; i < 500; i++)
        {
            var pagina = await ListarAsync(query, ct);
            pagina.Elementos.Count.Should().BeLessThanOrEqualTo(1);
            vistos.AddRange(pagina.Elementos.Select(e => e.JornadaId));
            if (pagina.SiguienteCursor is null) break;
            query = $"?take=1&cursor={Uri.EscapeDataString(pagina.SiguienteCursor)}";
        }

        vistos.Should().OnlyHaveUniqueItems();
        vistos.Should().Contain([id1, id2]);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarLimitesDeJornada_MaterializaLaPredeterminadaUnaSolaVez_CuandoSeListaSinPreferencias()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");
        var ct = TestContext.Current.CancellationToken;
        var schema = PostgresFixture.SchemaProgramacion;
        var streamPreferencias = PostgresFixture.StreamIdPreferencias(_tenant.TenantId);

        var lista = await Polling.WaitUntilAsync(async () =>
        {
            var l = await ListarAsync(_tenant.Client, "", ct);
            return l.Elementos.Any(e => EsPredeterminadaInicial(e)) ? l : null;
        }, Timeout);

        lista.Elementos.Should().Contain(e => EsPredeterminadaInicial(e));
        (await postgres.ContarEventosAsync(schema, streamPreferencias, "jornada_predeterminada_asignada"))
            .Should().Be(1);
        (await postgres.ContarEventosDeStreamAsync(schema, _tenant.TenantId, streamPreferencias))
            .Should().Be(1);

        var asignada = (await postgres.LeerEventosDeStreamAsync(schema, _tenant.TenantId, streamPreferencias))
            .Single();
        var predeterminadaId = asignada.GetProperty("JornadaId").GetGuid();
        lista.Elementos.Should().ContainSingle(e => e.JornadaId == predeterminadaId);

        await ListarAsync(_tenant.Client, "", ct);

        (await postgres.ContarEventosDeStreamAsync(schema, _tenant.TenantId, streamPreferencias))
            .Should().Be(1, "un segundo GET no debe escribir eventos");
        (await postgres.ContarEventosAsync(schema, predeterminadaId.ToString(), "jornada_creada"))
            .Should().Be(1);
    }

    private static bool EsPredeterminadaInicial(ElementoSmoke e) =>
        e.HorasSemanales == new HorasYMinutosSmoke(42, 0)
        && e.TopeDiario == new HorasYMinutosSmoke(8, 0)
        && e.MinimoDiario == new HorasYMinutosSmoke(0, 0)
        && e.DiasDescansoPorSemana == 1;

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarLimitesDeJornada_DebeMarcarLaPredeterminada_CuandoSeAsignaOtra()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = await CrearJornadaAsync(LimitesDePrueba.ListarMarcaPredeterminada, ct);

        var asignada = await _client.PutAsJsonAsync(
            "/api/programacion/preferencias/jornada-predeterminada", new { jornadaId = id }, ct);
        asignada.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var obtenida = await _client.GetAsync($"{Ruta}/{id}", ct);
        obtenida.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await obtenida.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("esPredeterminada").GetBoolean().Should().BeTrue();

        var lista = await Polling.WaitUntilAsync(async () =>
        {
            var l = await ListarAsync("", ct);
            return l.Elementos.Any(e => e.JornadaId == id) ? l : null;
        }, Timeout);

        lista.Elementos.Where(e => e.EsPredeterminada).Select(e => e.JornadaId)
            .Should().ContainSingle().Which.Should().Be(id);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarLimitesDeJornada_DebeRetornar400_CuandoElCursorEsInvalido()
    {
        var response = await _client.GetAsync($"{Ruta}?cursor=no-es-un-cursor",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
