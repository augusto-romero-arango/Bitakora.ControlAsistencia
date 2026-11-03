using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.ListarBorradoresDeProgramacion;

public class ListarBorradoresDeProgramacionSmokeTests(ApiFixture api)
{
    private readonly HttpClient _client = api.Client;

    private static readonly HttpMethod MetodoQuery = new("QUERY");

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record ListaSmoke(JsonElement Elementos, string? SiguienteCursor);

    private async Task<HttpResponseMessage> ConsultarAsync(object? filtro, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(MetodoQuery, "/api/programacion/borradores")
        {
            Content = JsonContent.Create(filtro)
        };
        return await _client.SendAsync(request, ct);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task Health_RetornaOk()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarBorradoresDeProgramacion_Retorna200ConElSobre_CuandoSeEnviaFechaReferencia()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarAsync(
            new { fechaReferencia = "2027-05-01", colaboradores = new[] { Guid.CreateVersion7().ToString() } }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var lista = await response.Content.ReadFromJsonAsync<ListaSmoke>(JsonOptions, ct);
        lista!.Elementos.ValueKind.Should().Be(JsonValueKind.Array);
        lista.Elementos.GetArrayLength().Should().Be(0);
        lista.SiguienteCursor.Should().BeNull();
    }

    private sealed record ColaboradorSmoke(string CodigoColaborador);

    private sealed record PaginaSmoke(IReadOnlyList<ColaboradorSmoke> Elementos, string? SiguienteCursor);

    private async Task<PaginaSmoke> ConsultarPaginaAsync(object filtro, CancellationToken ct)
    {
        var response = await ConsultarAsync(filtro, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<PaginaSmoke>(JsonOptions, ct))!;
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarBorradoresDeProgramacion_RecorreConCursorDosColaboradores_CuandoAmbosTienenBorrador()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoId = Guid.CreateVersion7();
        (await _client.PostAsJsonAsync("/api/programacion/turnos", new
        {
            turnoId,
            nombre = $"[TEST] Turno Smoke Cursor Borradores {turnoId}",
            ordinarias = new[]
            {
                new
                {
                    inicio = "08:00:00",
                    fin = "16:00:00",
                    descansos = Array.Empty<object>(),
                    extras = Array.Empty<object>()
                }
            }
        }, ct)).StatusCode.Should().Be(HttpStatusCode.Created);

        var codigoA = Guid.CreateVersion7().ToString();
        var codigoB = Guid.CreateVersion7().ToString();
        foreach (var (codigo, nombre) in new[] { (codigoA, "[TEST] Cursor Borrador A"), (codigoB, "[TEST] Cursor Borrador B") })
        {
            var solicitud = await _client.PostAsJsonAsync("/api/programacion/solicitudes", new
            {
                id = Guid.CreateVersion7(),
                turnoId,
                colaborador = new { identificacion = "CC-746746746", codigoColaborador = codigo, nombreCompleto = nombre },
                fechas = new[] { "2027-03-01" }
            }, ct);
            solicitud.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var filtro = new
        {
            fechaReferencia = "2027-03-01",
            colaboradores = new[] { codigoA, codigoB },
            take = 1
        };

        PaginaSmoke primera = new([], null);
        var materializada = await Polling.WaitUntilTrueAsync(async () =>
        {
            primera = await ConsultarPaginaAsync(filtro, ct);
            return primera.SiguienteCursor is not null;
        }, TimeSpan.FromSeconds(30));
        materializada.Should().BeTrue("los dos borradores del arrange deben estar materializados en la vista");

        primera.Elementos.Should().ContainSingle().Which.CodigoColaborador.Should().Be(codigoA);

        var segunda = await ConsultarPaginaAsync(new
        {
            fechaReferencia = "2027-03-01",
            colaboradores = new[] { codigoA, codigoB },
            take = 1,
            cursor = primera.SiguienteCursor
        }, ct);

        segunda.Elementos.Should().ContainSingle().Which.CodigoColaborador.Should().Be(codigoB);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarBorradoresDeProgramacion_Retorna422_CuandoElCursorEsInvalido()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarAsync(
            new { fechaReferencia = "2027-05-01", cursor = "no-es-un-cursor" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarBorradoresDeProgramacion_Retorna422_CuandoFaltaFechaReferencia()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarAsync(new { desde = "2027-05-01", hasta = "2027-05-31" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarBorradoresDeProgramacion_Retorna422_CuandoDesdeEsPosteriorAHasta()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarAsync(
            new { fechaReferencia = "2027-05-01", desde = "2027-05-31", hasta = "2027-05-01" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarBorradoresDeProgramacion_Retorna400_CuandoElBodyNoEsJsonValido()
    {
        var ct = TestContext.Current.CancellationToken;
        using var request = new HttpRequestMessage(MetodoQuery, "/api/programacion/borradores")
        {
            Content = new StringContent("{ esto no es json valido", Encoding.UTF8, "application/json")
        };

        var response = await _client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
