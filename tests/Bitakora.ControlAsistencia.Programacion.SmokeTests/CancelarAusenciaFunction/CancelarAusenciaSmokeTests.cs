using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.CancelarAusenciaFunction;

public class CancelarAusenciaSmokeTests(
    ApiFixture api, ServiceBusFixture serviceBus, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string TopicSalida = "ausencia-diaria-cancelada";
    private const string Suscripcion = "smoke-tests";
    private const string SchemaProgramacion = "programacion";
    private const string TipoEventoAusenciaCancelada = "ausencia_cancelada";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static string RutaAusencias(string codigo) =>
        $"/api/programacion/colaboradores/{codigo}/ausencias";

    private static string RutaCancelar(string codigo, object id) => $"{RutaAusencias(codigo)}/{id}:cancelar";

    private static object PayloadProgramar(Guid id, string fechaInicio, string fechaFin) => new
    {
        id,
        identificacion = "CC-744744744",
        nombreCompleto = "[TEST] Cancelar Ausencia Smoke",
        fechaInicio,
        fechaFin,
        motivo = "Vacaciones"
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
    public async Task CancelarAusencia_Retorna204YPersisteYPublicaUnEventoPorFecha_CuandoFechasEstanVigentes()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigo = Guid.CreateVersion7().ToString();
        var ausenciaId = Guid.CreateVersion7();
        var canceladas = new[] { "2027-05-11", "2027-05-12" };

        var programar = await _client.PostAsJsonAsync(
            RutaAusencias(codigo), PayloadProgramar(ausenciaId, "2027-05-10", "2027-05-12"), ct);
        programar.StatusCode.Should().Be(HttpStatusCode.Created);

        await serviceBus.PurgeAsync(TopicSalida, Suscripcion);

        var response = await _client.PostAsJsonAsync(
            RutaCancelar(codigo, ausenciaId), new { fechas = canceladas }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();

        var existe = await postgres.ExisteEventoAsync(
            SchemaProgramacion, $"ac:{codigo}", TipoEventoAusenciaCancelada, Timeout,
            campoJson: "AusenciaId", valorJson: ausenciaId.ToString());
        existe.Should().BeTrue("AusenciaCancelada debe quedar persistida en el stream del colaborador");

        var recibidos = new List<AusenciaDiariaCancelada>();
        for (var i = 0; i < canceladas.Length; i++)
        {
            recibidos.Add(await serviceBus.WaitForMessageAsync<AusenciaDiariaCancelada>(
                TopicSalida, Suscripcion, e => e.AusenciaId == ausenciaId, Timeout));
        }

        recibidos.Select(e => e.Fecha.ToString("yyyy-MM-dd")).Should().BeEquivalentTo(canceladas);
        recibidos.Should().OnlyContain(e => e.Colaborador.CodigoColaborador == codigo);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CancelarAusencia_Retorna204SinPublicar_CuandoFechasYaEstabanCanceladas()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;
        var codigo = Guid.CreateVersion7().ToString();
        var ausenciaId = Guid.CreateVersion7();
        var fechas = new[] { "2027-06-08", "2027-06-09" };

        var programar = await _client.PostAsJsonAsync(
            RutaAusencias(codigo), PayloadProgramar(ausenciaId, "2027-06-07", "2027-06-09"), ct);
        programar.StatusCode.Should().Be(HttpStatusCode.Created);

        await serviceBus.PurgeAsync(TopicSalida, Suscripcion);

        var primera = await _client.PostAsJsonAsync(
            RutaCancelar(codigo, ausenciaId), new { fechas }, ct);
        primera.StatusCode.Should().Be(HttpStatusCode.NoContent);

        for (var i = 0; i < fechas.Length; i++)
        {
            await serviceBus.WaitForMessageAsync<AusenciaDiariaCancelada>(
                TopicSalida, Suscripcion, e => e.AusenciaId == ausenciaId, Timeout);
        }

        var repetida = await _client.PostAsJsonAsync(
            RutaCancelar(codigo, ausenciaId), new { fechas }, ct);
        repetida.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Assert.ThrowsAsync<TimeoutException>(() =>
            serviceBus.WaitForMessageAsync<AusenciaDiariaCancelada>(
                TopicSalida, Suscripcion, e => e.AusenciaId == ausenciaId, TimeSpan.FromSeconds(3)));
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CancelarAusencia_Retorna204_CuandoFechasSonAjenasALaAusencia()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigo = Guid.CreateVersion7().ToString();
        var ausenciaId = Guid.CreateVersion7();

        var programar = await _client.PostAsJsonAsync(
            RutaAusencias(codigo), PayloadProgramar(ausenciaId, "2027-07-05", "2027-07-06"), ct);
        programar.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await _client.PostAsJsonAsync(
            RutaCancelar(codigo, ausenciaId), new { fechas = new[] { "2027-07-20" } }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CancelarAusencia_Retorna204_CuandoUnaNuevaAusenciaOcupaLasFechasCanceladas()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigo = Guid.CreateVersion7().ToString();
        var ausenciaId = Guid.CreateVersion7();

        var programar = await _client.PostAsJsonAsync(
            RutaAusencias(codigo), PayloadProgramar(ausenciaId, "2027-08-02", "2027-08-06"), ct);
        programar.StatusCode.Should().Be(HttpStatusCode.Created);

        var cancelar = await _client.PostAsJsonAsync(
            RutaCancelar(codigo, ausenciaId), new { fechas = new[] { "2027-08-04", "2027-08-05" } }, ct);
        cancelar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var nueva = await _client.PostAsJsonAsync(
            RutaAusencias(codigo), PayloadProgramar(Guid.CreateVersion7(), "2027-08-04", "2027-08-05"), ct);

        nueva.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CancelarAusencia_Retorna404_CuandoColaboradorNoTieneAusencias()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync(
            RutaCancelar(Guid.CreateVersion7().ToString(), Guid.CreateVersion7()),
            new { fechas = new[] { "2027-09-01" } }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CancelarAusencia_Retorna404_CuandoElIdNoExisteParaElColaborador()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigo = Guid.CreateVersion7().ToString();

        var programar = await _client.PostAsJsonAsync(
            RutaAusencias(codigo), PayloadProgramar(Guid.CreateVersion7(), "2027-09-06", "2027-09-07"), ct);
        programar.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await _client.PostAsJsonAsync(
            RutaCancelar(codigo, Guid.CreateVersion7()),
            new { fechas = new[] { "2027-09-06" } }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CancelarAusencia_Retorna400_CuandoListaDeFechasEstaVacia()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync(
            RutaCancelar(Guid.CreateVersion7().ToString(), Guid.CreateVersion7()),
            new { fechas = Array.Empty<string>() }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CancelarAusencia_Retorna400_CuandoFaltaLaListaDeFechas()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync(
            RutaCancelar(Guid.CreateVersion7().ToString(), Guid.CreateVersion7()),
            new { }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CancelarAusencia_Retorna400_CuandoElIdNoEsGuid()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync(
            RutaCancelar(Guid.CreateVersion7().ToString(), "no-es-un-guid"),
            new { fechas = new[] { "2027-09-01" } }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CancelarAusencia_Retorna400_CuandoCodigoNoEsUrlSafe()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync(
            RutaCancelar("codigo%20con%20espacios", Guid.CreateVersion7()),
            new { fechas = new[] { "2027-09-01" } }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
