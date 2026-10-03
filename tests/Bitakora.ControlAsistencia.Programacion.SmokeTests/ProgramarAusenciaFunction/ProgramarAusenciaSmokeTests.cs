using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.ProgramarAusenciaFunction;

public class ProgramarAusenciaSmokeTests(
    ApiFixture api, ServiceBusFixture serviceBus, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string TopicSalida = "ausencia-diaria-programada";
    private const string Suscripcion = "smoke-tests";
    private const string SchemaProgramacion = "programacion";
    private const string TipoEventoAusenciaProgramada = "ausencia_programada";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static string Ruta(string codigo) => $"/api/programacion/colaboradores/{codigo}/ausencias";

    private static object Payload(Guid id, string fechaInicio, string fechaFin, string motivo = "Vacaciones") => new
    {
        id,
        identificacion = "CC-743743743",
        nombreCompleto = "[TEST] Ausencia Smoke",
        fechaInicio,
        fechaFin,
        motivo
    };

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
    public async Task ProgramarAusencia_Retorna201YPersisteYPublicaUnEventoPorFecha_CuandoPayloadEsValido()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        await serviceBus.PurgeAsync(TopicSalida, Suscripcion);

        var codigo = Guid.CreateVersion7().ToString();
        var ausenciaId = Guid.CreateVersion7();
        var fechas = new[] { "2026-11-02", "2026-11-03", "2026-11-04" };

        var response = await _client.PostAsJsonAsync(
            Ruta(codigo), Payload(ausenciaId, fechas[0], fechas[^1], "IncapacidadMedica"), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var existe = await postgres.ExisteEventoAsync(
            SchemaProgramacion, $"ac:{codigo}", TipoEventoAusenciaProgramada, Timeout,
            campoJson: "AusenciaId", valorJson: ausenciaId.ToString());
        existe.Should().BeTrue("AusenciaProgramada debe quedar persistida en el stream del colaborador");

        var recibidos = new List<AusenciaDiariaProgramada>();
        for (var i = 0; i < fechas.Length; i++)
        {
            recibidos.Add(await serviceBus.WaitForMessageAsync<AusenciaDiariaProgramada>(
                TopicSalida, Suscripcion, e => e.AusenciaId == ausenciaId, Timeout));
        }

        recibidos.Select(e => e.Fecha.ToString("yyyy-MM-dd")).Should().BeEquivalentTo(fechas);
        recibidos.Should().OnlyContain(e => e.Motivo == "IncapacidadMedica");
        recibidos.Should().OnlyContain(e => e.Colaborador.CodigoColaborador == codigo);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ProgramarAusencia_Retorna201_CuandoRangoEsContiguoAUnaAusenciaPrevia()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigo = Guid.CreateVersion7().ToString();

        var primera = await _client.PostAsJsonAsync(
            Ruta(codigo), Payload(Guid.CreateVersion7(), "2026-12-10", "2026-12-19"), ct);
        var contigua = await _client.PostAsJsonAsync(
            Ruta(codigo), Payload(Guid.CreateVersion7(), "2026-12-20", "2026-12-22"), ct);

        primera.StatusCode.Should().Be(HttpStatusCode.Created);
        contigua.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ProgramarAusencia_Retorna409_CuandoRangoChocaConAusenciaVigente()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigo = Guid.CreateVersion7().ToString();

        var primera = await _client.PostAsJsonAsync(
            Ruta(codigo), Payload(Guid.CreateVersion7(), "2027-01-10", "2027-01-15"), ct);
        var choque = await _client.PostAsJsonAsync(
            Ruta(codigo), Payload(Guid.CreateVersion7(), "2027-01-15", "2027-01-18"), ct);

        primera.StatusCode.Should().Be(HttpStatusCode.Created);
        choque.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ProgramarAusencia_Retorna409_CuandoElIdYaExisteParaElColaborador()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigo = Guid.CreateVersion7().ToString();
        var ausenciaId = Guid.CreateVersion7();

        var primera = await _client.PostAsJsonAsync(
            Ruta(codigo), Payload(ausenciaId, "2027-02-01", "2027-02-02"), ct);
        var repetida = await _client.PostAsJsonAsync(
            Ruta(codigo), Payload(ausenciaId, "2027-03-01", "2027-03-02"), ct);

        primera.StatusCode.Should().Be(HttpStatusCode.Created);
        repetida.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ProgramarAusencia_Retorna400_CuandoFechaFinEsAnteriorAFechaInicio()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync(
            Ruta(Guid.CreateVersion7().ToString()),
            Payload(Guid.CreateVersion7(), "2027-04-10", "2027-04-09"), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ProgramarAusencia_Retorna400_CuandoMotivoNoEstaEnLaLista()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync(
            Ruta(Guid.CreateVersion7().ToString()),
            Payload(Guid.CreateVersion7(), "2027-04-10", "2027-04-11", "[TEST] Motivo Inexistente"), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ProgramarAusencia_Retorna400_CuandoFaltanCamposObligatorios()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new { id = Guid.CreateVersion7(), fechaInicio = "2027-04-10", fechaFin = "2027-04-11" };

        var response = await _client.PostAsJsonAsync(Ruta(Guid.CreateVersion7().ToString()), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ProgramarAusencia_Retorna400_CuandoCodigoNoEsUrlSafe()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync(
            Ruta("codigo%20con%20espacios"),
            Payload(Guid.CreateVersion7(), "2027-04-10", "2027-04-11"), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
