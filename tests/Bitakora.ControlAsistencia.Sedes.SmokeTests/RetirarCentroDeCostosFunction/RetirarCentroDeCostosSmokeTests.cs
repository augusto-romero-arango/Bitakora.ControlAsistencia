using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Sedes.SmokeTests.RetirarCentroDeCostosFunction;

// CentroDeCostosRetirado no cruza el bus en este issue: la unica verificacion black-box de los
// efectos del handler es leer mt_events via PostgresFixture -- no hay ServiceBusFixture que
// consultar.
public class RetirarCentroDeCostosSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/sedes";
    private const string SchemaSedes = "sedes";
    private const string TipoEventoSedeRegistrada = "sede_registrada";
    private const string TipoEventoCentroDeCostosAsignado = "centro_de_costos_asignado";
    private const string TipoEventoCentroDeCostosRetirado = "centro_de_costos_retirado";
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
            $"el evento {TipoEventoSedeRegistrada} deberia existir en el stream {streamId} antes de retirar el centro de costos");

        return codigo;
    }

    private async Task<string> RegistrarSedeConCentroDeCostosAsync(CancellationToken ct)
    {
        var codigo = await RegistrarSedeDePruebaAsync(ct);
        var streamId = ComputarStreamId(codigo);

        var asignacion = await _client.PutAsJsonAsync(
            RutaCentroDeCostos(codigo), new { centroDeCostos = "CC-VIGENTE" }, ct);
        asignacion.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que la asignacion previa funcione");

        var existeAsignacion = await postgres.ExisteEventoAsync(
            SchemaSedes, streamId, TipoEventoCentroDeCostosAsignado, Timeout,
            campoJson: "CentroDeCostos", valorJson: "CC-VIGENTE");
        existeAsignacion.Should().BeTrue(
            $"el evento {TipoEventoCentroDeCostosAsignado} deberia existir en el stream {streamId} antes de retirarlo");

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

    // CA-3: DELETE con CC vigente persiste CentroDeCostosRetirado.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarCentroDeCostos_Retorna202YPersisteCentroDeCostosRetirado_CuandoHayCentroDeCostosVigente()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigo = await RegistrarSedeConCentroDeCostosAsync(ct);

        var response = await _client.DeleteAsync(RutaCentroDeCostos(codigo), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = ComputarStreamId(codigo);
        var existe = await postgres.ExisteEventoAsync(
            SchemaSedes, streamId, TipoEventoCentroDeCostosRetirado, Timeout);

        existe.Should().BeTrue(
            $"el evento {TipoEventoCentroDeCostosRetirado} deberia existir en el stream {streamId}");
    }

    // CA-4: DELETE sin CC vigente declina -> 409, sin evento (CA-ADR-0030).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarCentroDeCostos_Retorna409YNoPersisteEvento_CuandoNoHayCentroDeCostosVigente()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigo = await RegistrarSedeDePruebaAsync(ct);
        var streamId = ComputarStreamId(codigo);

        var response = await _client.DeleteAsync(RutaCentroDeCostos(codigo), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var registros = await postgres.ContarEventosAsync(
            SchemaSedes, streamId, TipoEventoCentroDeCostosRetirado);
        registros.Should().Be(0,
            "la declinacion por 409 no debe haber persistido un evento de retiro (CA-ADR-0030)");
    }

    // El charset URL-safe del codigo tambien rige cuando viaja en la ruta: "!" queda fuera del set
    // unreserved y se rechaza con 400, nunca con el 404 de un stream inexistente.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarCentroDeCostos_Retorna400_CuandoCodigoDeRutaNoEsUrlSafe()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.DeleteAsync(
            RutaCentroDeCostos($"TEST!{Guid.CreateVersion7()}"), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-5: sede inexistente -> 404.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarCentroDeCostos_Retorna404_CuandoSedeNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.DeleteAsync(RutaCentroDeCostos(NuevoCodigo()), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
