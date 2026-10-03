using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.ListarAusenciasDelEquipo;

public class ListarAusenciasDelEquipoSmokeTests(ApiFixture api)
{
    private readonly HttpClient _client = api.Client;

    private static readonly HttpMethod MetodoQuery = new("QUERY");
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record TramoSmoke(DateOnly Desde, DateOnly Hasta);

    private sealed record AusenciaSmoke(Guid Id, string Motivo, IReadOnlyList<TramoSmoke> Tramos);

    private sealed record ColaboradorSmoke(
        string CodigoColaborador,
        string NombreCompleto,
        IReadOnlyList<AusenciaSmoke> Ausencias);

    private sealed record ListaSmoke(
        DateOnly Desde,
        DateOnly Hasta,
        bool RangoRecortado,
        IReadOnlyList<ColaboradorSmoke> Colaboradores);

    private async Task<HttpResponseMessage> ConsultarAsync(object? filtro, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(MetodoQuery, "/api/programacion/ausencias")
        {
            Content = JsonContent.Create(filtro)
        };
        return await _client.SendAsync(request, ct);
    }

    private async Task<HttpResponseMessage> ProgramarAsync(
        string codigo, Guid id, string nombre, string fechaInicio, string fechaFin, string motivo, CancellationToken ct) =>
        await _client.PostAsJsonAsync($"/api/programacion/colaboradores/{codigo}/ausencias", new
        {
            id,
            identificacion = "CC-755755755",
            nombreCompleto = nombre,
            fechaInicio,
            fechaFin,
            motivo
        }, ct);

    private async Task<ListaSmoke?> ConsultarListaAsync(object filtro, CancellationToken ct)
    {
        var response = await ConsultarAsync(filtro, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<ListaSmoke>(JsonOptions, ct);
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
    public async Task ListarAusenciasDelEquipo_Retorna200ConColaboradoresYTramosRecortados_CuandoHayAusenciasEnElPeriodo()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigoAna = Guid.CreateVersion7().ToString();
        var codigoLuis = Guid.CreateVersion7().ToString();
        var ausenciaAna = Guid.CreateVersion7();
        var ausenciaLuis = Guid.CreateVersion7();

        (await ProgramarAsync(codigoAna, ausenciaAna, "[TEST] Ana Equipo Smoke", "2027-09-01", "2027-09-30", "Vacaciones", ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await ProgramarAsync(codigoLuis, ausenciaLuis, "[TEST] Luis Equipo Smoke", "2027-09-15", "2027-09-16", "IncapacidadMedica", ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var filtro = new { desde = "2027-09-13", hasta = "2027-09-19", colaboradores = new[] { codigoAna, codigoLuis } };
        ListaSmoke? lista = null;
        var encontrado = await Polling.WaitUntilTrueAsync(async () =>
        {
            lista = await ConsultarListaAsync(filtro, ct);
            return lista!.Colaboradores.Count == 2;
        }, Timeout);

        encontrado.Should().BeTrue("la proyeccion AusenciaVigente deberia materializar ambas ausencias");
        lista!.RangoRecortado.Should().BeFalse();
        lista.Colaboradores.Select(c => c.CodigoColaborador).Should().Equal(codigoAna, codigoLuis);
        var ana = lista.Colaboradores[0];
        ana.Ausencias.Should().ContainSingle(a => a.Id == ausenciaAna)
            .Which.Tramos.Should().Equal(new TramoSmoke(new DateOnly(2027, 9, 13), new DateOnly(2027, 9, 19)));
        var luis = lista.Colaboradores[1];
        luis.Ausencias.Should().ContainSingle(a => a.Id == ausenciaLuis)
            .Which.Tramos.Should().Equal(new TramoSmoke(new DateOnly(2027, 9, 15), new DateOnly(2027, 9, 16)));
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAusenciasDelEquipo_RetornaSoloElColaboradorFiltrado_CuandoSeEnviaListaDeCodigos()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigoAna = Guid.CreateVersion7().ToString();
        var codigoLuis = Guid.CreateVersion7().ToString();

        (await ProgramarAsync(codigoAna, Guid.CreateVersion7(), "[TEST] Ana Filtro Smoke", "2027-10-04", "2027-10-08", "Vacaciones", ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await ProgramarAsync(codigoLuis, Guid.CreateVersion7(), "[TEST] Luis Filtro Smoke", "2027-10-04", "2027-10-08", "Vacaciones", ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var filtro = new { desde = "2027-10-04", hasta = "2027-10-10", colaboradores = new[] { codigoLuis } };
        ListaSmoke? lista = null;
        var encontrado = await Polling.WaitUntilTrueAsync(async () =>
        {
            lista = await ConsultarListaAsync(filtro, ct);
            return lista!.Colaboradores.Count > 0;
        }, Timeout);

        encontrado.Should().BeTrue("la proyeccion AusenciaVigente deberia materializar la ausencia");
        lista!.Colaboradores.Select(c => c.CodigoColaborador).Should().Equal(codigoLuis);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAusenciasDelEquipo_RetornaRangoRecortado_CuandoElPeriodoSuperaTreintaYUnDias()
    {
        var ct = TestContext.Current.CancellationToken;

        var lista = await ConsultarListaAsync(
            new { desde = "2027-01-01", hasta = "2027-03-31", colaboradores = new[] { Guid.CreateVersion7().ToString() } }, ct);

        lista!.RangoRecortado.Should().BeTrue();
        lista.Desde.Should().Be(new DateOnly(2027, 1, 1));
        lista.Hasta.Should().Be(new DateOnly(2027, 1, 31));
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAusenciasDelEquipo_Retorna200ConListaVacia_CuandoElPeriodoNoTieneAusencias()
    {
        var ct = TestContext.Current.CancellationToken;

        var lista = await ConsultarListaAsync(
            new { desde = "2027-05-01", hasta = "2027-05-07", colaboradores = new[] { Guid.CreateVersion7().ToString() } }, ct);

        lista!.Colaboradores.Should().BeEmpty();
        lista.RangoRecortado.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAusenciasDelEquipo_Retorna422_CuandoFaltaDesdeOHasta()
    {
        var ct = TestContext.Current.CancellationToken;

        var sinHasta = await ConsultarAsync(new { desde = "2027-05-01" }, ct);
        var sinDesde = await ConsultarAsync(new { hasta = "2027-05-31" }, ct);

        sinHasta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        sinDesde.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAusenciasDelEquipo_Retorna422_CuandoDesdeEsPosteriorAHasta()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarAsync(new { desde = "2027-05-31", hasta = "2027-05-01" }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAusenciasDelEquipo_Retorna400_CuandoElBodyNoEsJsonValido()
    {
        var ct = TestContext.Current.CancellationToken;
        using var request = new HttpRequestMessage(MetodoQuery, "/api/programacion/ausencias")
        {
            Content = new StringContent("{ esto no es json valido", Encoding.UTF8, "application/json")
        };

        var response = await _client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarAusenciasDelEquipo_Retorna415_CuandoContentTypeNoEsJson()
    {
        var ct = TestContext.Current.CancellationToken;
        using var request = new HttpRequestMessage(MetodoQuery, "/api/programacion/ausencias")
        {
            Content = new StringContent("{}", Encoding.UTF8, "text/plain")
        };

        var response = await _client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }
}
