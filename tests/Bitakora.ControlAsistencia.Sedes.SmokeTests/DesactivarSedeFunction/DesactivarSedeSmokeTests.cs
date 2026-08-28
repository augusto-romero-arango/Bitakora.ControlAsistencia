// SedeDesactivada no cruza el bus: la unica verificacion black-box de los efectos del handler es
// leer mt_events via PostgresFixture -- no hay ServiceBusFixture que consultar.
using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Sedes.SmokeTests.DesactivarSedeFunction;

public class DesactivarSedeSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/sedes";
    private const string SchemaSedes = "sedes";
    private const string TipoEventoSedeRegistrada = "sede_registrada";
    private const string TipoEventoSedeDesactivada = "sede_desactivada";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Prefijo "TEST-" y no "[TEST] ": el Codigo viaja en la ruta y esta sujeto al charset URL-safe,
    // del que "[", "]" y el espacio quedan fuera.
    private static string NuevoCodigo() => $"TEST-{Guid.CreateVersion7()}";

    // Recomputo local del streamId: oraculo independiente, sin referenciar ComputarStreamId.
    private static string ComputarStreamId(string codigo) => $"s:{codigo}";

    private static string RutaDesactivar(string codigo) => $"/api/sedes/{codigo}:desactivar";

    // La sede nace activa (sin evento inicial de activacion): registrarla ya deja el arrange en el
    // estado que CA-1 necesita.
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
            $"el evento {TipoEventoSedeRegistrada} deberia existir en el stream {streamId} antes de desactivar");

        return codigo;
    }

    private async Task<string> RegistrarSedeInactivaAsync(CancellationToken ct)
    {
        var codigo = await RegistrarSedeDePruebaAsync(ct);
        var streamId = ComputarStreamId(codigo);

        var desactivacion = await _client.PostAsync(RutaDesactivar(codigo), null, ct);
        desactivacion.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que la primera desactivacion funcione");

        var existeDesactivacion = await postgres.ExisteEventoAsync(
            SchemaSedes, streamId, TipoEventoSedeDesactivada, Timeout);
        existeDesactivacion.Should().BeTrue(
            $"el evento {TipoEventoSedeDesactivada} deberia existir en el stream {streamId} antes de reintentar");

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

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DesactivarSede_Retorna202YPersisteSedeDesactivada_CuandoSedeEstaActiva()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigo = await RegistrarSedeDePruebaAsync(ct);

        var response = await _client.PostAsync(RutaDesactivar(codigo), null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = ComputarStreamId(codigo);
        var existe = await postgres.ExisteEventoAsync(
            SchemaSedes, streamId, TipoEventoSedeDesactivada, Timeout);

        existe.Should().BeTrue(
            $"el evento {TipoEventoSedeDesactivada} deberia existir en el stream {streamId}");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DesactivarSede_Retorna409YNoDuplicaEvento_CuandoSedeYaEstaInactiva()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigo = await RegistrarSedeInactivaAsync(ct);
        var streamId = ComputarStreamId(codigo);

        var response = await _client.PostAsync(RutaDesactivar(codigo), null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var registros = await postgres.ContarEventosAsync(
            SchemaSedes, streamId, TipoEventoSedeDesactivada);
        registros.Should().Be(1,
            "la declinacion por 409 no debe haber escrito un segundo sede_desactivada (CA-ADR-0030)");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DesactivarSede_Retorna404_CuandoSedeNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsync(RutaDesactivar(NuevoCodigo()), null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // El charset URL-safe del codigo tambien rige cuando viaja en la ruta: "!" queda fuera del set
    // unreserved y se rechaza con 400, nunca con el 404 de un stream inexistente.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DesactivarSede_Retorna400_CuandoCodigoDeRutaNoEsUrlSafe()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsync(RutaDesactivar($"TEST!{Guid.CreateVersion7()}"), null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
