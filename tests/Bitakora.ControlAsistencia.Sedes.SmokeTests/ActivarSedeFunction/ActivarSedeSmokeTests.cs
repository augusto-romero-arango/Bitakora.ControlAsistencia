// Issue #459: smoke tests de POST sedes/{codigo}:activar. SedeActivada no cruza el bus en este
// issue: la unica verificacion black-box de los efectos del handler es leer mt_events via
// PostgresFixture -- no hay ServiceBusFixture que consultar.
using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Sedes.SmokeTests.ActivarSedeFunction;

public class ActivarSedeSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/sedes";
    private const string SchemaSedes = "sedes";
    private const string TipoEventoSedeRegistrada = "sede_registrada";
    private const string TipoEventoSedeActivada = "sede_activada";
    private const string TipoEventoSedeDesactivada = "sede_desactivada";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Prefijo "TEST-" y no "[TEST] ": el Codigo viaja en la ruta y esta sujeto al charset URL-safe,
    // del que "[", "]" y el espacio quedan fuera.
    private static string NuevoCodigo() => $"TEST-{Guid.CreateVersion7()}";

    // Recomputo local del streamId: oraculo independiente, sin referenciar ComputarStreamId.
    private static string ComputarStreamId(string codigo) => $"s:{codigo}";

    private static string RutaActivar(string codigo) => $"/api/sedes/{codigo}:activar";
    private static string RutaDesactivar(string codigo) => $"/api/sedes/{codigo}:desactivar";

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
            $"el evento {TipoEventoSedeRegistrada} deberia existir en el stream {streamId} antes de activar");

        return codigo;
    }

    // La sede nace activa (sin evento inicial de activacion): para llegar a "inactiva" hay que
    // desactivarla primero.
    private async Task<string> RegistrarSedeInactivaAsync(CancellationToken ct)
    {
        var codigo = await RegistrarSedeDePruebaAsync(ct);
        var streamId = ComputarStreamId(codigo);

        var desactivacion = await _client.PostAsync(RutaDesactivar(codigo), null, ct);
        desactivacion.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que la desactivacion previa funcione");

        var existeDesactivacion = await postgres.ExisteEventoAsync(
            SchemaSedes, streamId, TipoEventoSedeDesactivada, Timeout);
        existeDesactivacion.Should().BeTrue(
            $"el evento {TipoEventoSedeDesactivada} deberia existir en el stream {streamId} antes de reactivar");

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

    // CA-2
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ActivarSede_Retorna202YPersisteSedeActivada_CuandoSedeEstaInactiva()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigo = await RegistrarSedeInactivaAsync(ct);

        var response = await _client.PostAsync(RutaActivar(codigo), null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = ComputarStreamId(codigo);
        var existe = await postgres.ExisteEventoAsync(
            SchemaSedes, streamId, TipoEventoSedeActivada, Timeout);

        existe.Should().BeTrue(
            $"el evento {TipoEventoSedeActivada} deberia existir en el stream {streamId}");
    }

    // CA-3: aplica tambien a una sede recien registrada (nace activa, sin evento inicial).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ActivarSede_Retorna409YNoPersisteEvento_CuandoSedeYaEstaActiva()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigo = await RegistrarSedeDePruebaAsync(ct);
        var streamId = ComputarStreamId(codigo);

        var response = await _client.PostAsync(RutaActivar(codigo), null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var registros = await postgres.ContarEventosAsync(
            SchemaSedes, streamId, TipoEventoSedeActivada);
        registros.Should().Be(0,
            "la declinacion por 409 no debe haber persistido un evento de activacion (CA-ADR-0030)");
    }

    // CA-5
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ActivarSede_Retorna404_CuandoSedeNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsync(RutaActivar(NuevoCodigo()), null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // El charset URL-safe del codigo tambien rige cuando viaja en la ruta: "!" queda fuera del set
    // unreserved y se rechaza con 400, nunca con el 404 de un stream inexistente.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ActivarSede_Retorna400_CuandoCodigoDeRutaNoEsUrlSafe()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsync(RutaActivar($"TEST!{Guid.CreateVersion7()}"), null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
