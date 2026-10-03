using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.ListarAusenciasColaborador;

public class ListarAusenciasColaboradorSmokeTests(ApiFixture api)
{
    private readonly HttpClient _client = api.Client;

    private static readonly HttpMethod MetodoQuery = new("QUERY");

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record TramoSmoke(DateOnly Desde, DateOnly Hasta);

    private sealed record AusenciaSmoke(
        Guid Id,
        string Motivo,
        DateOnly FechaInicio,
        DateOnly FechaFin,
        IReadOnlyList<TramoSmoke> TramosVigentes);

    private static string Ruta(string codigo) => $"/api/programacion/colaboradores/{codigo}/ausencias";

    private async Task<HttpResponseMessage> ConsultarAsync(string codigo, object? filtro, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(MetodoQuery, Ruta(codigo))
        {
            Content = JsonContent.Create(filtro)
        };
        return await _client.SendAsync(request, ct);
    }

    private async Task<HttpResponseMessage> ProgramarAsync(
        string codigo, Guid id, string fechaInicio, string fechaFin, CancellationToken ct) =>
        await _client.PostAsJsonAsync(Ruta(codigo), new
        {
            id,
            identificacion = "CC-750750750",
            nombreCompleto = "[TEST] Listar Ausencias Smoke",
            fechaInicio,
            fechaFin,
            motivo = "Vacaciones"
        }, ct);

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
    public async Task ListarAusenciasColaborador_Retorna200ConAusenciaYTramoCompleto_CuandoElColaboradorTieneUnaAusenciaSinCancelaciones()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigo = Guid.CreateVersion7().ToString();
        var ausenciaId = Guid.CreateVersion7();

        var registro = await ProgramarAsync(codigo, ausenciaId, "2027-05-13", "2027-05-26", ct);
        registro.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await ConsultarAsync(codigo, new { desde = "2027-05-01", hasta = "2027-05-31" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var ausencias = await response.Content.ReadFromJsonAsync<List<AusenciaSmoke>>(JsonOptions, ct);
        var ausencia = ausencias.Should().ContainSingle(a => a.Id == ausenciaId).Subject;
        ausencia.Motivo.Should().Be("Vacaciones");
        ausencia.FechaInicio.Should().Be(new DateOnly(2027, 5, 13));
        ausencia.FechaFin.Should().Be(new DateOnly(2027, 5, 26));
        ausencia.TramosVigentes.Should().Equal(new TramoSmoke(new DateOnly(2027, 5, 13), new DateOnly(2027, 5, 26)));
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAusenciasColaborador_RetornaSoloLasQueSeCruzanConElRangoOrdenadasPorInicio_CuandoHayVariasAusencias()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigo = Guid.CreateVersion7().ToString();
        var tardia = Guid.CreateVersion7();
        var temprana = Guid.CreateVersion7();
        var fueraDelRango = Guid.CreateVersion7();

        (await ProgramarAsync(codigo, tardia, "2027-06-20", "2027-06-25", ct)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await ProgramarAsync(codigo, temprana, "2027-06-05", "2027-06-10", ct)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await ProgramarAsync(codigo, fueraDelRango, "2027-08-01", "2027-08-05", ct)).StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await ConsultarAsync(codigo, new { desde = "2027-06-08", hasta = "2027-06-30" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var ausencias = await response.Content.ReadFromJsonAsync<List<AusenciaSmoke>>(JsonOptions, ct);
        ausencias!.Select(a => a.Id).Should().Equal(temprana, tardia);
        ausencias![0].TramosVigentes.Should().Equal(new TramoSmoke(new DateOnly(2027, 6, 5), new DateOnly(2027, 6, 10)));
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAusenciasColaborador_Retorna200ConListaVacia_CuandoElColaboradorNoTieneAusencias()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarAsync(
            Guid.CreateVersion7().ToString(), new { desde = "2027-05-01", hasta = "2027-05-31" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var ausencias = await response.Content.ReadFromJsonAsync<List<AusenciaSmoke>>(JsonOptions, ct);
        ausencias.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAusenciasColaborador_Retorna422_CuandoFaltaDesdeOHasta()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigo = Guid.CreateVersion7().ToString();

        var sinHasta = await ConsultarAsync(codigo, new { desde = "2027-05-01" }, ct);
        var sinDesde = await ConsultarAsync(codigo, new { hasta = "2027-05-31" }, ct);

        sinHasta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        sinDesde.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAusenciasColaborador_Retorna422_CuandoDesdeEsPosteriorAHasta()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarAsync(
            Guid.CreateVersion7().ToString(), new { desde = "2027-05-31", hasta = "2027-05-01" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAusenciasColaborador_Retorna400_CuandoCodigoNoEsUrlSafe()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarAsync(
            "codigo%20con%20espacios", new { desde = "2027-05-01", hasta = "2027-05-31" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAusenciasColaborador_Retorna400_CuandoElBodyNoEsJsonValido()
    {
        var ct = TestContext.Current.CancellationToken;
        using var request = new HttpRequestMessage(MetodoQuery, Ruta(Guid.CreateVersion7().ToString()))
        {
            Content = new StringContent("{ esto no es json valido", Encoding.UTF8, "application/json")
        };

        var response = await _client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAusenciasColaborador_Retorna415_CuandoContentTypeNoEsJson()
    {
        var ct = TestContext.Current.CancellationToken;
        using var request = new HttpRequestMessage(MetodoQuery, Ruta(Guid.CreateVersion7().ToString()))
        {
            Content = new StringContent("{}", Encoding.UTF8, "text/plain")
        };

        var response = await _client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }
}
