using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Sedes.SmokeTests.AsignarCentroDeCostosFunction;

// CentroDeCostosAsignado no cruza el bus en este issue: la unica verificacion black-box de los
// efectos del handler es leer mt_events via PostgresFixture -- no hay ServiceBusFixture que
// consultar.
public class AsignarCentroDeCostosSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/sedes";
    private const string SchemaSedes = "sedes";
    private const string TipoEventoSedeRegistrada = "sede_registrada";
    private const string TipoEventoCentroDeCostosAsignado = "centro_de_costos_asignado";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Prefijo "TEST-" y no "[TEST] ": el Codigo viaja en la ruta y esta sujeto al charset URL-safe,
    // del que "[", "]" y el espacio quedan fuera.
    private static string NuevoCodigo() => $"TEST-{Guid.CreateVersion7()}";

    // Recomputo local del streamId: oraculo independiente, sin referenciar ComputarStreamId.
    private static string ComputarStreamId(string codigo) => $"s:{codigo}";

    private static string RutaCentroDeCostos(string codigo) => $"/api/sedes/{codigo}/centro-de-costos";

    private async Task<string> RegistrarSedeDePruebaAsync(CancellationToken ct)
    {
        var codigo = NuevoCodigo();
        var payload = new { codigo, nombre = "[TEST] Sede Original", ciudad = (string?)null, direccion = (string?)null };

        var response = await _client.PostAsJsonAsync(RutaRegistrar, payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que el registro previo funcione");

        var streamId = ComputarStreamId(codigo);
        var existe = await postgres.ExisteEventoAsync(
            SchemaSedes, streamId, TipoEventoSedeRegistrada, Timeout);
        existe.Should().BeTrue(
            $"el evento {TipoEventoSedeRegistrada} deberia existir en el stream {streamId} antes de asignar el centro de costos");

        return codigo;
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // CA-1: PUT con CC valido persiste el string opaco tal cual, sin normalizacion.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarCentroDeCostos_Retorna202YPersisteCentroDeCostosAsignado_CuandoCentroDeCostosEsValido()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigo = await RegistrarSedeDePruebaAsync(ct);

        var payload = new { centroDeCostos = "CC-001" };
        var response = await _client.PutAsJsonAsync(RutaCentroDeCostos(codigo), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = ComputarStreamId(codigo);
        var existe = await postgres.ExisteEventoAsync(
            SchemaSedes, streamId, TipoEventoCentroDeCostosAsignado, Timeout,
            campoJson: "CentroDeCostos", valorJson: "CC-001");

        existe.Should().BeTrue(
            $"el evento {TipoEventoCentroDeCostosAsignado} deberia existir en el stream {streamId}");

        var evento = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaSedes, streamId, TipoEventoCentroDeCostosAsignado,
            "CentroDeCostos", "CC-001", TimeSpan.FromSeconds(5));

        evento.GetProperty("CentroDeCostos").GetString().Should().Be("CC-001");
    }

    // CA-2: PUT sobre una sede que ya tiene CC persiste un nuevo CentroDeCostosAsignado (reemplazo,
    // mismo comando -- PUT semantico, MEF-ADR-0043 paso 2).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarCentroDeCostos_Retorna202YPersisteSegundoEvento_CuandoYaTieneCentroDeCostosVigente()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigo = await RegistrarSedeDePruebaAsync(ct);
        var streamId = ComputarStreamId(codigo);

        var primeraAsignacion = await _client.PutAsJsonAsync(
            RutaCentroDeCostos(codigo), new { centroDeCostos = "CC-ORIGINAL" }, ct);
        primeraAsignacion.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que la primera asignacion funcione");

        var existePrimeraAsignacion = await postgres.ExisteEventoAsync(
            SchemaSedes, streamId, TipoEventoCentroDeCostosAsignado, Timeout,
            campoJson: "CentroDeCostos", valorJson: "CC-ORIGINAL");
        existePrimeraAsignacion.Should().BeTrue(
            $"el primer {TipoEventoCentroDeCostosAsignado} deberia estar en el stream {streamId} antes de reemplazar");

        var segundaAsignacion = await _client.PutAsJsonAsync(
            RutaCentroDeCostos(codigo), new { centroDeCostos = "CC-REEMPLAZO" }, ct);

        segundaAsignacion.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var existeReemplazo = await postgres.ExisteEventoAsync(
            SchemaSedes, streamId, TipoEventoCentroDeCostosAsignado, Timeout,
            campoJson: "CentroDeCostos", valorJson: "CC-REEMPLAZO");
        existeReemplazo.Should().BeTrue(
            $"el reemplazo deberia persistir un nuevo {TipoEventoCentroDeCostosAsignado} con el valor actualizado");

        var registros = await postgres.ContarEventosAsync(
            SchemaSedes, streamId, TipoEventoCentroDeCostosAsignado);
        registros.Should().Be(2,
            "asignar por primera vez y reemplazar son el mismo comando: cada PUT agrega su propio evento");
    }

    // CA-5: CC vacio -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarCentroDeCostos_Retorna400_CuandoCentroDeCostosEsVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new { centroDeCostos = "" };

        var response = await _client.PutAsJsonAsync(RutaCentroDeCostos(NuevoCodigo()), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // El charset URL-safe del codigo tambien rige cuando viaja en la ruta: "!" queda fuera del set
    // unreserved y se rechaza con 400, nunca con el 404 de un stream inexistente.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarCentroDeCostos_Retorna400_CuandoCodigoDeRutaNoEsUrlSafe()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new { centroDeCostos = "CC-001" };

        var response = await _client.PutAsJsonAsync(
            RutaCentroDeCostos($"TEST!{Guid.CreateVersion7()}"), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-5: sede inexistente -> 404.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarCentroDeCostos_Retorna404_CuandoSedeNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new { centroDeCostos = "CC-001" };

        var response = await _client.PutAsJsonAsync(RutaCentroDeCostos(NuevoCodigo()), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
